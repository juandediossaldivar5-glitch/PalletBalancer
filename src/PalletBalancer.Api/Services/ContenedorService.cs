using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;
using PalletBalancer.Core.Models;

namespace PalletBalancer.Api.Services;

public class ContenedorService
{
    private const int FilasPorLado = 26;

    /// <summary>
    /// Calcula el plan de estiba para uno o varios FDOs.
    /// ordenDescarga: lista de consignees en orden de descarga (índice 0 = primero en descargar,
    /// cerca de puertas; último = último en descargar, cerca de cabina).
    /// Si es null se ordena automáticamente (alfabético).
    /// </summary>
    public ContenedorResultadoDto Calcular(IEnumerable<Fdo> fdos, List<string>? ordenDescarga = null)
    {
        var sinDatos = new List<string>();

        // Agrupar pallets y descripciones por consignee
        var palletsPorDestino = new Dictionary<string, List<Pallet>>(StringComparer.OrdinalIgnoreCase);
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

                var item = linea.Item;
                int pxp  = item.SpPiezasPorPallet;
                int full = linea.ReqQty / pxp;
                int rest = linea.ReqQty % pxp;

                for (int i = 0; i < full; i++)
                    palletsPorDestino[dest].Add(Build(linea.ModelNo, pxp,
                        item.SpPesoKg, item.SpLargoCm, item.SpAnchoCm, item.SpAltoCm));

                if (rest > 0)
                {
                    double pw = Math.Round((double)rest / pxp * item.SpPesoKg, 2);
                    double ah = Math.Round((double)rest / pxp * item.SpAltoCm, 1);
                    palletsPorDestino[dest].Add(Build(linea.ModelNo, rest,
                        pw, item.SpLargoCm, item.SpAnchoCm, ah > 0 ? ah : item.SpAltoCm));
                }
            }
        }

        // Determinar orden final (primer elemento = primero en descargar = filas de puertas)
        var destinosConPallets = palletsPorDestino
            .Where(kv => kv.Value.Count > 0)
            .Select(kv => kv.Key)
            .ToList();

        List<string> ordenFinal = ordenDescarga?.Where(d => destinosConPallets.Contains(d,
                StringComparer.OrdinalIgnoreCase)).ToList()
            ?? destinosConPallets.OrderBy(d => d).ToList();

        // Agregar cualquier destino no incluido en el orden (al final)
        foreach (var d in destinosConPallets.Where(d =>
            !ordenFinal.Any(o => string.Equals(o, d, StringComparison.OrdinalIgnoreCase))))
            ordenFinal.Add(d);

        // Asignar zonas de filas:
        // ordenFinal[0] → primero en descargar → filas ALTAS (cerca de puertas, fila 26 hacia arriba)
        // ordenFinal[last] → último en descargar → filas BAJAS (cerca de cabina, fila 1)
        // Invertimos para asignar desde la cabina hacia las puertas.
        var ordenCarga    = ((IEnumerable<string>)ordenFinal).Reverse().ToList(); // último descarga primero en cargarse
        var posiciones    = new List<PosicionResultadoDto>();
        var destinoInfos  = new List<DestinoInfoDto>();
        double pesoIzq    = 0, pesoDer = 0;
        int filaActual    = 1;

        foreach (var dest in ordenCarga)
        {
            var pallets = palletsPorDestino.GetValueOrDefault(dest, []);
            if (pallets.Count == 0) continue;

            // Filas necesarias: ceil(pallets / 2) porque cada fila tiene 2 lados
            int rowsNec  = (int)Math.Ceiling(pallets.Count / 2.0);
            int filaFin  = Math.Min(filaActual + rowsNec - 1, FilasPorLado);

            var (posDest, pIzq, pDer) = BalancearZona(
                pallets, dest, filaActual, filaFin, descMap);

            posiciones.AddRange(posDest);
            pesoIzq    += pIzq;
            pesoDer    += pDer;

            destinoInfos.Add(new DestinoInfoDto
            {
                Consignee     = dest,
                OrdenDescarga = ordenFinal.IndexOf(
                    ordenFinal.First(o => string.Equals(o, dest, StringComparison.OrdinalIgnoreCase))) + 1,
                TotalPallets  = pallets.Count,
                FilaInicio    = filaActual,
                FilaFin       = filaFin,
            });

            filaActual = filaFin + 1;
            if (filaActual > FilasPorLado) break;
        }

        double mayor    = Math.Max(pesoIzq, pesoDer);
        double menor    = Math.Min(pesoIzq, pesoDer);
        double difPorc  = mayor == 0 ? 0 : Math.Round((mayor - menor) / mayor * 100, 2);
        int    total    = posiciones.Count;

        var advertencias = new List<string>();
        if (difPorc > 5)
            advertencias.Add($"Diferencia de peso entre lados ({difPorc}%) excede la tolerancia (5%).");
        var pesoTotal = Math.Round(pesoIzq + pesoDer, 2);
        if (pesoTotal > 22_000)
            advertencias.Add($"Peso total ({pesoTotal} kg) excede la capacidad máxima del contenedor (22,000 kg).");
        if (filaActual > FilasPorLado + 1)
            advertencias.Add($"La carga requiere más posiciones de las disponibles en el contenedor.");

        return new ContenedorResultadoDto
        {
            Posiciones          = posiciones.OrderBy(p => p.Fila).ThenBy(p => p.Lado).ToList(),
            Destinos            = destinoInfos.OrderBy(d => d.OrdenDescarga).ToList(),
            PesoIzquierdoKg    = Math.Round(pesoIzq, 2),
            PesoDerechoKg      = Math.Round(pesoDer, 2),
            PesoTotalKg        = pesoTotal,
            DiferenciaPorcentual = difPorc,
            DentroDeTolerancia  = difPorc <= 5,
            Advertencias        = advertencias,
            TotalPallets        = total,
            ModelosSinDatos     = sinDatos,
        };
    }

    private static (List<PosicionResultadoDto> pos, double pesoIzq, double pesoDer)
        BalancearZona(List<Pallet> pallets, string destino, int filaInicio, int filaFin,
                      Dictionary<string, string> descMap)
    {
        var pos     = new List<PosicionResultadoDto>();
        var izq     = new List<Pallet>();
        var der     = new List<Pallet>();
        double pIzq = 0, pDer = 0;

        // Greedy L/R balance (heaviest first)
        foreach (var p in pallets.OrderByDescending(p => p.PesoTotalKg))
        {
            if (pIzq <= pDer) { izq.Add(p); pIzq += p.PesoTotalKg; }
            else              { der.Add(p); pDer += p.PesoTotalKg; }
        }

        int fila   = filaInicio;
        int maxFil = filaFin;

        for (int i = 0; i < Math.Max(izq.Count, der.Count); i++)
        {
            if (fila > maxFil) break;

            if (i < izq.Count)
                pos.Add(ToDto(izq[i], fila, "Izquierdo", destino, descMap));
            if (i < der.Count)
                pos.Add(ToDto(der[i], fila, "Derecho",   destino, descMap));

            fila++;
        }

        return (pos, pIzq, pDer);
    }

    private static PosicionResultadoDto ToDto(Pallet p, int fila, string lado,
        string destino, Dictionary<string, string> descMap) =>
        new()
        {
            Fila        = fila,
            Lado        = lado,
            Destino     = destino,
            ModelNo     = p.Sku,
            Descripcion = descMap.TryGetValue(p.Sku, out var d) ? d : "",
            PesoKg      = Math.Round(p.PesoTotalKg, 2),
            Piezas      = p.CantidadPiezas,
            Capas       = 1,
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
