using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;
using PalletBalancer.Core.Models;

namespace PalletBalancer.Api.Services;

public class ContenedorService
{
    // Límites NOM-012 México (kg) — T3-S2 con suspensión neumática, carreteras federales
    // W1: eje delantero con llantas radiales anchas (llanta super single / eje tipo ETA) → 8,000 kg
    // W2/Wr: tándem neumático → 18,000 kg por tándem
    private const double NOM_W1  =  8_000;
    private const double NOM_W2  = 18_000;
    private const double NOM_Wr  = 18_000;
    private const double NOM_GVW = 48_000;

    // Límites FHWA EE.UU. (kg)
    private const double FHWA_W1  =  5_443;
    private const double FHWA_W2  = 15_422;
    private const double FHWA_Wr  = 15_422;
    private const double FHWA_GVW = 36_287;

    // Margen de seguridad (RF-08): alerta cuando W_max > límite × (1 − margen)
    private const double MARGEN_SEGURIDAD = 0.02;

    // Incertidumbre de peso por pallet (variación real vs catálogo: tarima madera, humedad, etc.)
    // Se aplica a peor caso: todos los pallets +4 kg simultáneamente
    private const double TOLERANCIA_PALLET_KG = 4.0;

    public ContenedorResultadoDto Calcular(
        IEnumerable<Fdo> fdos,
        List<string>? ordenDescarga    = null,
        string?       tipoContenedor   = null,
        string?       tipoTractocamion = null)
    {
        var spec = ContenedorSpecs.Get(tipoContenedor);
        var trac = TractocamionSpecs.Get(tipoTractocamion);
        var sinDatos = new List<string>();

        var palletsPorDestino = new Dictionary<string, List<(Pallet P, bool Apilable, bool EsParcial)>>(StringComparer.OrdinalIgnoreCase);
        var descMap           = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fdo in fdos)
        {
            var dest = string.IsNullOrWhiteSpace(fdo.Consignee) ? "Sin destino" : fdo.Consignee;
            if (!palletsPorDestino.ContainsKey(dest))
                palletsPorDestino[dest] = [];

            foreach (var linea in fdo.Lineas)
            {
                if (linea.Item != null && !descMap.ContainsKey(linea.ModelNo))
                    descMap[linea.ModelNo] = linea.Item.Descripcion;

                if (linea.Item == null || linea.Item.SpPiezasPorPallet == 0)
                {
                    if (!sinDatos.Contains(linea.ModelNo)) sinDatos.Add(linea.ModelNo);
                    continue;
                }

                var item      = linea.Item;
                bool apilable = item.PuedeEstibar;
                int pxp  = item.SpPiezasPorPallet;
                int full = linea.ReqQty / pxp;
                int rest = linea.ReqQty % pxp;

                for (int i = 0; i < full; i++)
                    palletsPorDestino[dest].Add((Build(linea.ModelNo, pxp,
                        item.SpPesoKg, item.SpLargoCm, item.SpAnchoCm, item.SpAltoCm), apilable, false));

                if (rest > 0)
                {
                    double pw = Math.Round((double)rest / pxp * item.SpPesoKg, 2);
                    double ah = Math.Round((double)rest / pxp * item.SpAltoCm, 1);
                    palletsPorDestino[dest].Add((Build(linea.ModelNo, rest,
                        pw, item.SpLargoCm, item.SpAnchoCm, ah > 0 ? ah : item.SpAltoCm), apilable, true));
                }
            }
        }

        // Dimensión modal de tarima (para cálculo de filas y CG)
        var todosPallets = palletsPorDestino.Values.SelectMany(x => x).Select(x => x.P).ToList();
        double palletLargo = todosPallets
            .Where(p => p.LargoCm > 0)
            .GroupBy(p => p.LargoCm)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault(120.0);
        double palletAncho = todosPallets
            .Where(p => p.AnchoCm > 0)
            .GroupBy(p => p.AnchoCm)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault(100.0);

        int filasPorLado = palletLargo > 0
            ? (int)Math.Floor((double)spec.LargoCm / palletLargo)
            : 26;
        if (filasPorLado < 1) filasPorLado = 1;

        var destinosConPallets = palletsPorDestino
            .Where(kv => kv.Value.Count > 0)
            .Select(kv => kv.Key)
            .ToList();

        // Crear stacks: pares de pallets apilables que caben en el alto del contenedor
        var stacksPorDestino = new Dictionary<string, List<(Pallet Piso, Pallet? Encima, bool Apilable, bool PisoEsParcial, bool EncimaEsParcial)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (dest, lista) in palletsPorDestino)
        {
            var stacks      = new List<(Pallet Piso, Pallet? Encima, bool Apilable, bool PisoEsParcial, bool EncimaEsParcial)>();
            var apilables   = lista.Where(x => x.Apilable).OrderByDescending(x => x.P.PesoTotalKg).ToList();
            var noApilables = lista.Where(x => !x.Apilable).ToList();

            int i = 0;
            while (i < apilables.Count)
            {
                var piso = apilables[i];
                if (i + 1 < apilables.Count && piso.P.AltoCm + apilables[i + 1].P.AltoCm <= spec.AltoCm)
                {
                    stacks.Add((piso.P, apilables[i + 1].P, true, piso.EsParcial, apilables[i + 1].EsParcial));
                    i += 2;
                }
                else
                {
                    stacks.Add((piso.P, null, true, piso.EsParcial, false));
                    i++;
                }
            }
            foreach (var p in noApilables)
                stacks.Add((p.P, null, false, p.EsParcial, false));

            stacksPorDestino[dest] = stacks;
        }

        // Orden de descarga (índice 0 = primero en descargar = cerca de puertas)
        // Sin orden explícito: más pesado a puertas → reduce W2 (eje drive) al alejar el CG del king pin
        var pesoPorDestino = stacksPorDestino.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Sum(s => s.Piso.PesoTotalKg + (s.Encima?.PesoTotalKg ?? 0)));

        List<string> ordenFinal = ordenDescarga?.Where(d => destinosConPallets.Contains(d,
                StringComparer.OrdinalIgnoreCase)).ToList()
            ?? destinosConPallets.OrderByDescending(d => pesoPorDestino.GetValueOrDefault(d)).ToList();

        foreach (var d in destinosConPallets.Where(d =>
            !ordenFinal.Any(o => string.Equals(o, d, StringComparison.OrdinalIgnoreCase))))
            ordenFinal.Add(d);

        // Asignar filas: ordenFinal[last] → fila 1 (cabina), ordenFinal[0] → filas altas (puertas)
        var ordenCarga   = ((IEnumerable<string>)ordenFinal).Reverse().ToList();
        var posiciones   = new List<PosicionResultadoDto>();
        var destinoInfos = new List<DestinoInfoDto>();
        double pesoIzq   = 0, pesoDer = 0;
        int filaActual   = 1;

        // Total de filas requeridas por toda la carga
        int rowsTotalesNecesarios = stacksPorDestino.Values
            .Sum(sl => (int)Math.Ceiling(sl.Count / 2.0));

        foreach (var dest in ordenCarga)
        {
            var stacks = stacksPorDestino.GetValueOrDefault(dest, []);
            if (stacks.Count == 0) continue;

            if (filaActual > filasPorLado) break;

            int rowsNec = (int)Math.Ceiling(stacks.Count / 2.0);
            int filaFin = Math.Min(filaActual + rowsNec - 1, filasPorLado);

            var (posDest, pIzq, pDer) = BalancearZona(stacks, dest, filaActual, filaFin, descMap);

            posiciones.AddRange(posDest);
            pesoIzq  += pIzq;
            pesoDer  += pDer;

            int totalPallets = stacks.Sum(s => s.Encima is null ? 1 : 2);
            destinoInfos.Add(new DestinoInfoDto
            {
                Consignee     = dest,
                OrdenDescarga = ordenFinal.IndexOf(
                    ordenFinal.First(o => string.Equals(o, dest, StringComparison.OrdinalIgnoreCase))) + 1,
                TotalPallets  = totalPallets,
                FilaInicio    = filaActual,
                FilaFin       = filaFin,
            });

            filaActual = filaFin + 1;
        }

        // Balance L/R
        double mayor   = Math.Max(pesoIzq, pesoDer);
        double menor   = Math.Min(pesoIzq, pesoDer);
        double difPorc = mayor == 0 ? 0 : Math.Round((mayor - menor) / mayor * 100, 2);
        int    total   = posiciones.Count;

        var advertencias = new List<string>();
        if (difPorc > 5)
            advertencias.Add($"Diferencia de peso entre lados ({difPorc}%) excede la tolerancia (5%).");
        var pesoTotal = Math.Round(pesoIzq + pesoDer, 2);
        if (pesoTotal > spec.PayloadMaxKg)
            advertencias.Add($"Peso total de carga ({pesoTotal} kg) excede la capacidad de carga del contenedor {spec.Tipo} ({spec.PayloadMaxKg:N0} kg ISO).");
        if (rowsTotalesNecesarios > filasPorLado)
            advertencias.Add($"La carga requiere más posiciones ({rowsTotalesNecesarios}) de las disponibles en el contenedor {spec.Tipo} ({filasPorLado} filas).");
        if (palletAncho * 2 > spec.AnchoCm)
            advertencias.Add($"Dos tarimas lado a lado ({palletAncho * 2} cm) exceden el ancho interior del contenedor {spec.Tipo} ({spec.AnchoCm} cm).");

        // Cálculo de ejes con rango (RF-08)
        // NOM-012 se evalúa con el tractor seleccionado (T3-S2 mexicano)
        // FHWA se evalúa con US Class 8 (drayage en frontera EE.UU.) — mismo remolque, otro tractor
        var ejes = CalcularEjes(posiciones, palletLargo, spec, trac);
        var tracUs = TractocamionSpecs.Get("US Class 8 Day Cab");
        var ejesUs = trac.Tipo == tracUs.Tipo
            ? ejes
            : CalcularEjes(posiciones, palletLargo, spec, tracUs);

        // Estado de cumplimiento por eje y norma (cada uno con su tractor correspondiente)
        var estNomW1  = EstadoCumplimiento(ejes.w1Min,   ejes.w1Max,   NOM_W1);
        var estNomW2  = EstadoCumplimiento(ejes.w2Min,   ejes.w2Max,   NOM_W2);
        var estNomWr  = EstadoCumplimiento(ejes.wr,      ejes.wr,      NOM_Wr);
        var estFhwaW1 = EstadoCumplimiento(ejesUs.w1Min, ejesUs.w1Max, FHWA_W1);
        var estFhwaW2 = EstadoCumplimiento(ejesUs.w2Min, ejesUs.w2Max, FHWA_W2);
        var estFhwaWr = EstadoCumplimiento(ejesUs.wr,    ejesUs.wr,    FHWA_Wr);

        // Advertencias: Falla bloqueante, Condicional como alerta
        void WarnEje(string estado, string eje, double max, double lim, string norma)
        {
            if (estado == "Falla")
                advertencias.Add($"⛔ {eje} FALLA en báscula — máx. posible {max:N0} kg excede {norma} ({lim:N0} kg).");
            else if (estado == "Condicional")
                advertencias.Add($"⚠ {eje} CONDICIONAL — puede exceder {norma} ({lim:N0} kg) con tanque lleno (máx. {max:N0} kg).");
        }

        WarnEje(estFhwaW1, "Eje delantero", ejesUs.w1Max, FHWA_W1, "FHWA");
        WarnEje(estFhwaW2, "Eje tractor",   ejesUs.w2Max, FHWA_W2, "FHWA");
        WarnEje(estFhwaWr, "Eje remolque",  ejesUs.wr,    FHWA_Wr, "FHWA");
        if (ejesUs.gvwMax > FHWA_GVW) advertencias.Add($"GVW máx. posible ({ejesUs.gvwMax:N0} kg) excede FHWA ({FHWA_GVW:N0} kg).");
        WarnEje(estNomW1,  "Eje delantero", ejes.w1Max, NOM_W1,  "NOM-012");
        WarnEje(estNomW2,  "Eje tractor",   ejes.w2Max, NOM_W2,  "NOM-012");
        WarnEje(estNomWr,  "Eje remolque",  ejes.wr,    NOM_Wr,  "NOM-012");
        if (ejes.gvwMax > NOM_GVW) advertencias.Add($"GVW máx. posible ({ejes.gvwMax:N0} kg) excede NOM-012 ({NOM_GVW:N0} kg).");

        return new ContenedorResultadoDto
        {
            Posiciones              = posiciones.OrderBy(p => p.Fila).ThenBy(p => p.Lado).ThenBy(p => p.Capa).ToList(),
            Destinos                = destinoInfos.OrderBy(d => d.OrdenDescarga).ToList(),
            PesoIzquierdoKg        = Math.Round(pesoIzq, 2),
            PesoDerechoKg          = Math.Round(pesoDer, 2),
            PesoTotalKg            = pesoTotal,
            DiferenciaPorcentual   = difPorc,
            DentroDeTolerancia     = difPorc <= 5,
            Advertencias           = advertencias,
            TotalPallets           = total,
            ModelosSinDatos        = sinDatos,
            ContenedorTipo         = spec.Tipo,
            ContenedorLargoCm      = spec.LargoCm,
            ContenedorAnchoCm      = spec.AnchoCm,
            ContenedorAltoCm       = spec.AltoCm,
            FilasDisponibles       = filasPorLado,
            PalletLargoCm          = palletLargo,
            PalletAnchoCm          = palletAncho,
            // Ejes — punto de referencia = peor caso (Max)
            TractocamionTipo       = trac.Tipo,
            CgLongitudinalCm       = ejes.cgCm,
            CgLongitudinalPct      = ejes.cgPct,
            PesoEjeDelanteroKg     = ejes.w1Max,
            PesoEjeTractorKg       = ejes.w2Max,
            PesoEjeRemolqueKg      = ejes.wr,
            PesoTotalGVWKg         = ejes.gvwMax,
            // Rangos (RF-08)
            PesoEjeDelanteroMinKg  = ejes.w1Min,
            PesoEjeDelanteroMaxKg  = ejes.w1Max,
            PesoEjeTractorMinKg    = ejes.w2Min,
            PesoEjeTractorMaxKg    = ejes.w2Max,
            PesoTotalGVWMinKg      = ejes.gvwMin,
            PesoTotalGVWMaxKg      = ejes.gvwMax,
            EstadoNomW1            = estNomW1,
            EstadoNomW2            = estNomW2,
            EstadoNomWr            = estNomWr,
            EstadoFhwaW1           = estFhwaW1,
            EstadoFhwaW2           = estFhwaW2,
            EstadoFhwaWr           = estFhwaWr,
            MargenSeguridadPct     = MARGEN_SEGURIDAD * 100,
            TolerancePesoTotalKg   = posiciones.Count * TOLERANCIA_PALLET_KG,
            // Ejes evaluados con US Class 8 (frontera)
            PesoEjeDelanteroUsMinKg = ejesUs.w1Min,
            PesoEjeDelanteroUsMaxKg = ejesUs.w1Max,
            PesoEjeTractorUsMinKg   = ejesUs.w2Min,
            PesoEjeTractorUsMaxKg   = ejesUs.w2Max,
            PesoTotalGVWUsMinKg     = ejesUs.gvwMin,
            PesoTotalGVWUsMaxKg     = ejesUs.gvwMax,
        };
    }

    // "Seguro"      → W_max ≤ límite × (1 − margen)  — margen completo, no fallará ni con incertidumbre
    // "Condicional" → dentro del margen pero W_min ≤ límite legal  — al límite, puede pasar
    // "Falla"       → W_min > límite legal  — falla incluso en el mejor escenario
    private static string EstadoCumplimiento(double min, double max, double limite)
    {
        double limEfectivo = limite * (1 - MARGEN_SEGURIDAD);
        if (max <= limEfectivo) return "Seguro";
        if (min <= limite)      return "Condicional";
        return "Falla";
    }

    // Calcula CG longitudinal y cargas por eje con rango mínimo/máximo (RF-08).
    // Wr es determinístico (depende solo de la carga, no del tractor).
    // W1 y W2 tienen rango según tara mínima/máxima del tractocamión.
    private static (double w1Min, double w1Max, double w2Min, double w2Max,
                    double wr, double gvwMin, double gvwMax,
                    double cgCm, double cgPct)
        CalcularEjes(List<PosicionResultadoDto> posiciones, double palletLargo,
                     ContenedorSpec spec, TractocamionSpec trac)
    {
        double wCargo = posiciones.Sum(p => p.PesoKg);

        // CG de la carga medido desde el king pin (positivo = hacia puertas)
        // Fila 1 empieza en el frente del contenedor, que está KingPinOffsetCm ANTES del king pin.
        // centro de fila F = (F - 0.5) × palletLargo - KingPinOffsetCm
        double cgCm = wCargo > 0
            ? posiciones.Sum(p => p.PesoKg * ((p.Fila - 0.5) * palletLargo - spec.KingPinOffsetCm)) / wCargo
            : spec.KingPinAEjeCm / 2.0;

        double wTara = spec.TaraChassisKg + spec.TaraContenedorKg;
        double L     = spec.KingPinAEjeCm;

        // Incertidumbre de peso: todos los pallets pueden ser +/- TOLERANCIA_PALLET_KG
        // Peor caso simultáneo (todos más pesados): tolCargo = N × tol.
        // Se asume distribución similar → CG no cambia, solo la magnitud de la carga.
        double tolCargo = posiciones.Count * TOLERANCIA_PALLET_KG;
        double wCargoMin = Math.Max(0, wCargo - tolCargo);
        double wCargoMax = wCargo + tolCargo;

        // Viga simplemente apoyada: king pin (x=0) y eje remolque (x=L)
        // Wr con rango: depende de wCargo (que ahora es rango) y del CG
        double wrMin = (wCargoMin * cgCm + wTara * (L / 2.0)) / L;
        double wrMax = (wCargoMax * cgCm + wTara * (L / 2.0)) / L;
        double wr    = (wCargo    * cgCm + wTara * (L / 2.0)) / L;   // nominal
        // fkp peor caso: max cuando cargo max y wr min → max carga sobre 5a rueda
        double fkpMin = wCargoMin + wTara - wrMax;
        double fkpMax = wCargoMax + wTara - wrMin;

        // Modelo tractor: eje delantero (x=0), eje trasero (x=WB), quinta rueda (x=WB-Q)
        double wb = trac.WheelbaseCm;
        double q  = trac.QuintaRuedaCm;    // distancia quinta rueda ADELANTE del eje trasero

        double w2Min = trac.TaraEjeTraseroMinKg   + fkpMin * (wb - q) / wb;
        double w2Max = trac.TaraEjeTraseroMaxKg   + fkpMax * (wb - q) / wb;
        double w1Min = trac.TaraEjeDelanteroMinKg + fkpMin * q / wb;
        double w1Max = trac.TaraEjeDelanteroMaxKg + fkpMax * q / wb;

        double gvwMin = w1Min + w2Min + wrMin;
        double gvwMax = w1Max + w2Max + wrMax;
        double cgPct  = L > 0 ? Math.Round(cgCm / L * 100, 1) : 50.0;

        return (Math.Round(w1Min, 0), Math.Round(w1Max, 0),
                Math.Round(w2Min, 0), Math.Round(w2Max, 0),
                Math.Round(wrMax, 0),   // reporta peor caso Wr
                Math.Round(gvwMin, 0), Math.Round(gvwMax, 0),
                Math.Round(cgCm, 0), cgPct);
    }

    private static (List<PosicionResultadoDto> pos, double pesoIzq, double pesoDer)
        BalancearZona(List<(Pallet Piso, Pallet? Encima, bool Apilable, bool PisoEsParcial, bool EncimaEsParcial)> stacks,
                      string destino, int filaInicio, int filaFin, Dictionary<string, string> descMap)
    {
        var pos     = new List<PosicionResultadoDto>();
        var izq     = new List<(Pallet Piso, Pallet? Encima, bool Apilable, bool PisoEsParcial, bool EncimaEsParcial)>();
        var der     = new List<(Pallet Piso, Pallet? Encima, bool Apilable, bool PisoEsParcial, bool EncimaEsParcial)>();
        double pIzq = 0, pDer = 0;

        foreach (var s in stacks.OrderByDescending(s => s.Piso.PesoTotalKg + (s.Encima?.PesoTotalKg ?? 0)))
        {
            double w = s.Piso.PesoTotalKg + (s.Encima?.PesoTotalKg ?? 0);
            if (pIzq <= pDer) { izq.Add(s); pIzq += w; }
            else              { der.Add(s); pDer += w; }
        }

        int fila   = filaInicio;
        int maxFil = filaFin;

        for (int i = 0; i < Math.Max(izq.Count, der.Count); i++)
        {
            if (fila > maxFil) break;

            if (i < izq.Count)
            {
                pos.Add(ToDto(izq[i].Piso, fila, "Izquierdo", destino, descMap, 1, izq[i].PisoEsParcial, izq[i].Apilable));
                if (izq[i].Encima is not null)
                    pos.Add(ToDto(izq[i].Encima!, fila, "Izquierdo", destino, descMap, 2, izq[i].EncimaEsParcial, izq[i].Apilable));
            }
            if (i < der.Count)
            {
                pos.Add(ToDto(der[i].Piso, fila, "Derecho", destino, descMap, 1, der[i].PisoEsParcial, der[i].Apilable));
                if (der[i].Encima is not null)
                    pos.Add(ToDto(der[i].Encima!, fila, "Derecho", destino, descMap, 2, der[i].EncimaEsParcial, der[i].Apilable));
            }

            fila++;
        }

        return (pos, pIzq, pDer);
    }

    private static PosicionResultadoDto ToDto(Pallet p, int fila, string lado,
        string destino, Dictionary<string, string> descMap, int capa = 1,
        bool esParcial = false, bool apilable = true) =>
        new()
        {
            Fila        = fila,
            Lado        = lado,
            Destino     = destino,
            ModelNo     = p.Sku,
            Descripcion = descMap.TryGetValue(p.Sku, out var d) ? d : "",
            PesoKg      = Math.Round(p.PesoTotalKg, 2),
            Piezas      = p.CantidadPiezas,
            Capa        = capa,
            AltoCm      = p.AltoCm,
            LargoCm     = p.LargoCm,
            AnchoCm     = p.AnchoCm,
            EsParcial   = esParcial,
            Apilable    = apilable,
        };

    private static Pallet Build(string sku, int piezas, double pesoKg,
        double largoCm, double anchoCm, double altoCm) =>
        new()
        {
            CodigoEscaneado = sku,
            Sku             = sku,
            CantidadPiezas  = piezas,
            PesoTotalKg     = pesoKg,
            LargoCm         = largoCm > 0 ? largoCm : 120,
            AnchoCm         = anchoCm > 0 ? anchoCm : 120,
            AltoCm          = altoCm  > 0 ? altoCm  : 66,
        };
}
