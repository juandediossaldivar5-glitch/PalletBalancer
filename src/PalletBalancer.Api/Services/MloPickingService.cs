using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Services;

public static class MloPickingService
{
    /// <summary>
    /// Given the container plan positions and the MLOs for each FDO,
    /// returns an ordered picking list (row 1 first = deepest in container).
    /// </summary>
    public static PickingResultadoDto Calcular(
        ContenedorResultadoDto plan,
        List<(Fdo Fdo, Mlo? Mlo, Item? Item)> fdoData)
    {
        var advertencias = new List<string>();
        var lineas       = new List<PickingLineaDto>();

        // Build case pool: modelNo → sorted queue of MloLineas (closest location first)
        var poolRaw = new Dictionary<string, List<MloLinea>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, mlo, _) in fdoData)
        {
            if (mlo == null) continue;
            foreach (var linea in mlo.Lineas)
            {
                if (!poolRaw.ContainsKey(linea.ModelNo))
                    poolRaw[linea.ModelNo] = [];
                poolRaw[linea.ModelNo].Add(linea);
            }
        }

        var casePool = new Dictionary<string, Queue<MloLinea>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (modelNo, list) in poolRaw)
        {
            var sorted = list
                .OrderBy(l => MloXlsParser.ParseLocation(l.FromLocation).Rack)
                .ThenBy(l => MloXlsParser.ParseLocation(l.FromLocation).Pos)
                .ThenBy(l => MloXlsParser.ParseLocation(l.FromLocation).Level)
                .ToList();
            casePool[modelNo] = new Queue<MloLinea>(sorted);
        }

        // Build lookup: mloId → MLO (for MloNo resolution by owning MLO)
        var mloById = fdoData
            .Where(x => x.Mlo != null)
            .ToDictionary(x => x.Mlo!.Id, x => x.Mlo!);

        // Positions ordered: row ascending (1 = deepest = pick first), then lado, then capa
        var posOrdenadas = plan.Posiciones
            .OrderBy(p => p.Fila)
            .ThenBy(p => p.Lado)
            .ThenBy(p => p.Capa)
            .ToList();

        int orden = 1;
        foreach (var pos in posOrdenadas)
        {
            if (!casePool.TryGetValue(pos.ModelNo, out var pool) || pool.Count == 0)
            {
                advertencias.Add(
                    $"Sin CASE en MLO para modelo {pos.ModelNo} (Fila {pos.Fila}, {pos.Lado})");
                lineas.Add(new PickingLineaDto
                {
                    OrdenPicking   = orden++,
                    FilaContenedor = pos.Fila,
                    LadoContenedor = pos.Lado,
                    CapaContenedor = pos.Capa,
                    Consignee      = pos.Destino,
                    ModelNo        = pos.ModelNo,
                    Descripcion    = pos.Descripcion,
                    Qty            = pos.Piezas,
                    EsParcial      = pos.EsParcial,
                    CaseNo         = "(sin MLO)",
                    FromLocation   = "(sin MLO)",
                });
                continue;
            }

            var case_ = pool.Dequeue();

            // Resolve which MLO this case belongs to via its owning MloId
            var mloRef = mloById.GetValueOrDefault(case_.MloId);

            lineas.Add(new PickingLineaDto
            {
                OrdenPicking   = orden++,
                FilaContenedor = pos.Fila,
                LadoContenedor = pos.Lado,
                CapaContenedor = pos.Capa,
                Consignee      = pos.Destino,
                FdoSlipNo      = case_.SlipNo,
                MloNo          = mloRef?.MloNo ?? "",
                CaseNo         = case_.CaseNo,
                FromLocation   = case_.FromLocation,
                ModelNo        = pos.ModelNo,
                Descripcion    = pos.Descripcion,
                Qty            = pos.Piezas,
                EsParcial      = pos.EsParcial,
            });
        }

        return new PickingResultadoDto { Lineas = lineas, Advertencias = advertencias };
    }
}
