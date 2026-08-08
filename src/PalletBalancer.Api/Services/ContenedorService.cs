using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;
using PalletBalancer.Core.Models;

namespace PalletBalancer.Api.Services;

public class ContenedorService
{
    // Límites NOM-012 México (kg) — carreteras federales estándar
    private const double NOM_W1  =  6_000;
    private const double NOM_W2  = 16_000;
    private const double NOM_Wr  = 16_000;
    private const double NOM_GVW = 40_500;

    // Límites FHWA EE.UU. (kg)
    private const double FHWA_W1  =  5_443;
    private const double FHWA_W2  = 15_422;
    private const double FHWA_Wr  = 15_422;
    private const double FHWA_GVW = 36_287;

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
        List<string> ordenFinal = ordenDescarga?.Where(d => destinosConPallets.Contains(d,
                StringComparer.OrdinalIgnoreCase)).ToList()
            ?? destinosConPallets.OrderBy(d => d).ToList();

        foreach (var d in destinosConPallets.Where(d =>
            !ordenFinal.Any(o => string.Equals(o, d, StringComparison.OrdinalIgnoreCase))))
            ordenFinal.Add(d);

        // Asignar filas: ordenFinal[last] → fila 1 (cabina), ordenFinal[0] → filas altas (puertas)
        var ordenCarga   = ((IEnumerable<string>)ordenFinal).Reverse().ToList();
        var posiciones   = new List<PosicionResultadoDto>();
        var destinoInfos = new List<DestinoInfoDto>();
        double pesoIzq   = 0, pesoDer = 0;
        int filaActual   = 1;

        foreach (var dest in ordenCarga)
        {
            var stacks = stacksPorDestino.GetValueOrDefault(dest, []);
            if (stacks.Count == 0) continue;

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
            if (filaActual > filasPorLado) break;
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
        if (filaActual > filasPorLado + 1)
            advertencias.Add($"La carga requiere más posiciones ({filaActual - 1}) de las disponibles en el contenedor {spec.Tipo} ({filasPorLado} filas).");
        if (palletAncho * 2 > spec.AnchoCm)
            advertencias.Add($"Dos tarimas lado a lado ({palletAncho * 2} cm) exceden el ancho interior del contenedor {spec.Tipo} ({spec.AnchoCm} cm).");

        // Filas efectivamente usadas
        int filasUsadas = destinoInfos.Count > 0 ? destinoInfos.Max(d => d.FilaFin) : 0;

        // Cálculo de ejes
        var ejes = CalcularEjes(posiciones, palletLargo, spec, trac);

        if (ejes.w1 > FHWA_W1)  advertencias.Add($"Eje delantero ({ejes.w1:N0} kg) excede límite FHWA ({FHWA_W1:N0} kg).");
        if (ejes.w2 > FHWA_W2)  advertencias.Add($"Eje tractor ({ejes.w2:N0} kg) excede límite FHWA ({FHWA_W2:N0} kg).");
        if (ejes.wr > FHWA_Wr)  advertencias.Add($"Eje remolque ({ejes.wr:N0} kg) excede límite FHWA ({FHWA_Wr:N0} kg).");
        if (ejes.gvw > FHWA_GVW) advertencias.Add($"Peso bruto total GVW ({ejes.gvw:N0} kg) excede límite FHWA ({FHWA_GVW:N0} kg).");
        if (ejes.w1 > NOM_W1)   advertencias.Add($"Eje delantero ({ejes.w1:N0} kg) excede límite NOM-012 ({NOM_W1:N0} kg).");
        if (ejes.w2 > NOM_W2)   advertencias.Add($"Eje tractor ({ejes.w2:N0} kg) excede límite NOM-012 ({NOM_W2:N0} kg).");
        if (ejes.wr > NOM_Wr)   advertencias.Add($"Eje remolque ({ejes.wr:N0} kg) excede límite NOM-012 ({NOM_Wr:N0} kg).");
        if (ejes.gvw > NOM_GVW) advertencias.Add($"Peso bruto total GVW ({ejes.gvw:N0} kg) excede límite NOM-012 ({NOM_GVW:N0} kg).");

        // Seguridad de carga (huecos y equipo recomendado)
        var seguridad = CalcularSeguridad(filasUsadas, palletLargo, palletAncho, spec);

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
            // Ejes
            TractocamionTipo       = trac.Tipo,
            CgLongitudinalCm       = ejes.cgCm,
            CgLongitudinalPct      = ejes.cgPct,
            PesoEjeDelanteroKg     = ejes.w1,
            PesoEjeTractorKg       = ejes.w2,
            PesoEjeRemolqueKg      = ejes.wr,
            PesoTotalGVWKg         = ejes.gvw,
            // Seguridad de carga
            FilasUsadas            = filasUsadas,
            GapLongitudinalCm      = seguridad[0].GapCm,
            GapLateralCm           = seguridad[1].GapCm,
            SeguridadCarga         = seguridad,
        };
    }

    // Calcula CG longitudinal y cargas por eje
    private static (double w1, double w2, double wr, double gvw, double cgCm, double cgPct)
        CalcularEjes(List<PosicionResultadoDto> posiciones, double palletLargo,
                     ContenedorSpec spec, TractocamionSpec trac)
    {
        double wCargo = posiciones.Sum(p => p.PesoKg);

        // CG de la carga medido desde el king pin
        // fila 1 = cabina (cerca del king pin), fila N = puertas (lejos)
        // centro de fila F = KingPinOffset + (F - 0.5) × palletLargo
        double cgCm = wCargo > 0
            ? posiciones.Sum(p => p.PesoKg * (spec.KingPinOffsetCm + (p.Fila - 0.5) * palletLargo)) / wCargo
            : spec.KingPinAEjeCm / 2.0;

        double wTara = spec.TaraChassisKg + spec.TaraContenedorKg;
        double L     = spec.KingPinAEjeCm;

        // Modelo de viga simplemente apoyada: king pin (x=0) y eje remolque (x=L)
        // Carga de la tara del chassis+contenedor asumida en el centro (L/2)
        double wr  = (wCargo * cgCm + wTara * (L / 2.0)) / L;
        double fkp = wCargo + wTara - wr;   // reacción en quinta rueda

        // Modelo tractor: eje delantero (x=0), eje trasero (x=WB), quinta rueda (x=WB-Q)
        double wb  = trac.WheelbaseCm;
        double q   = trac.QuintaRuedaCm;    // distancia quinta rueda ADELANTE del eje trasero
        double w2  = trac.TaraEjeTraseroKg  + fkp * (wb - q) / wb;
        double w1  = trac.TaraEjeDelanteroKg + fkp * q / wb;
        double gvw = w1 + w2 + wr;

        double cgPct = L > 0 ? Math.Round(cgCm / L * 100, 1) : 50.0;

        return (Math.Round(w1, 0), Math.Round(w2, 0), Math.Round(wr, 0),
                Math.Round(gvw, 0), Math.Round(cgCm, 0), cgPct);
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

    private static List<SeguridadCargaItem> CalcularSeguridad(
        int filasUsadas, double palletLargo, double palletAncho, ContenedorSpec spec)
    {
        double gapLong = Math.Max(0, Math.Round(spec.LargoCm - filasUsadas * palletLargo, 0));
        double gapLat  = Math.Max(0, Math.Round((spec.AnchoCm - palletAncho * 2) / 2.0, 0));

        // --- Longitudinal ---
        string equL, descL, nivL;
        int    canL;
        if (gapLong <= 10)
        {
            equL = "Sin sujeción requerida"; nivL = "ninguno"; canL = 0;
            descL = $"Hueco de {gapLong:F0} cm — ajuste muy estrecho, sin riesgo de movimiento.";
        }
        else if (gapLong <= 30)
        {
            equL = "Cartón corrugado doble / tabla de madera"; nivL = "bajo"; canL = 1;
            descL = $"Hueco de {gapLong:F0} cm — rellenar con cartón corrugado doble o tabla de 3/4\" para inmovilizar.";
        }
        else if (gapLong <= 60)
        {
            equL = "Bolsa de aire Nivel 1 (kraft, 2–3 PSI)"; nivL = "medio";
            canL = (int)Math.Ceiling(gapLong / 45.0) * 2; // 2 columnas por ancho del contenedor
            descL = $"Hueco de {gapLong:F0} cm — bolsa kraft 48\"×48\", inflar a 2–3 PSI. Se requieren {canL} bolsas (2 columnas).";
        }
        else if (gapLong <= 120)
        {
            equL = "Bolsa de aire Nivel 2 (polywoven, 3–5 PSI)"; nivL = "medio";
            canL = (int)Math.Ceiling(gapLong / 45.0) * 2;
            descL = $"Hueco de {gapLong:F0} cm — bolsa polywoven 48\"×48\", inflar a 3–5 PSI. Se requieren {canL} bolsas (2 columnas).";
        }
        else
        {
            equL = "Bloqueo de madera 4\"×4\" + bolsa de aire Nivel 2"; nivL = "alto";
            canL = (int)Math.Ceiling(gapLong / 90.0);
            descL = $"Hueco grande de {gapLong:F0} cm — instalar bloqueo de madera contrachapada y bolsa polywoven. {canL} posición(es).";
        }

        // --- Lateral ---
        string equA, descA, nivA;
        int    canA;
        if (gapLat <= 8)
        {
            equA = "Sin sujeción requerida"; nivA = "ninguno"; canA = 0;
            descA = $"Hueco lateral de {gapLat:F0} cm por lado — ajuste estrecho, sin riesgo de desplazamiento lateral.";
        }
        else if (gapLat <= 20)
        {
            equA = "Foam board o cartón lateral"; nivA = "bajo"; canA = 2;
            descA = $"Hueco de {gapLat:F0} cm por lado — insertar foam board o cartón corrugado entre pallet y pared (1 pieza por lado).";
        }
        else
        {
            equA = "Bolsa de aire lateral o tabla de madera"; nivA = "medio"; canA = 2;
            descA = $"Hueco de {gapLat:F0} cm por lado — colocar bolsa de aire lateral o tabla 2\"×4\" en cada costado.";
        }

        return
        [
            new SeguridadCargaItem { Tipo="Longitudinal (cabina → puertas)", GapCm=gapLong, Nivel=nivL, Equipo=equL, Cantidad=canL, Descripcion=descL },
            new SeguridadCargaItem { Tipo="Lateral (por cada lado)",          GapCm=gapLat,  Nivel=nivA, Equipo=equA, Cantidad=canA, Descripcion=descA },
        ];
    }

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
