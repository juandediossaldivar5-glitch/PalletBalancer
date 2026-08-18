using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Services;

public static class MloPickingService
{
    // ── internal types ────────────────────────────────────────────────────────

    private sealed record PalletSlot(
        int          Qty,
        bool         EsParcial,
        bool         NecesitaConfirmacion,  // true = CASEs de distintas ubicaciones combinados
        string?      Recomendacion,
        string       PrimaryCaseNo,
        string       PrimaryLocation,
        string       FdoSlipNo,
        string       MloNo,
        List<string> CasesAdicionales);

    // ── public API ────────────────────────────────────────────────────────────

    public static PickingResultadoDto Calcular(
        ContenedorResultadoDto plan,
        List<(Fdo Fdo, Mlo? Mlo, Item? Item)> fdoData,
        string? modoOrden = null)
    {
        var advertencias = new List<string>();
        var lineas       = new List<PickingLineaDto>();

        // SpPiezasPorPallet per model (from FDO lineas that already have Item loaded)
        var spByModel = fdoData
            .SelectMany(x => x.Fdo.Lineas)
            .Where(l => l.Item?.SpPiezasPorPallet > 0)
            .GroupBy(l => l.ModelNo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Item!.SpPiezasPorPallet,
                          StringComparer.OrdinalIgnoreCase);

        // Mlo + Fdo owner by MloId
        var ownerByMloId = fdoData
            .Where(x => x.Mlo != null)
            .ToDictionary(x => x.Mlo!.Id, x => (Mlo: x.Mlo!, Fdo: x.Fdo));

        // All MloLineas grouped by ModelNo
        var lineaByModel = new Dictionary<string, List<MloLinea>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, mlo, _) in fdoData)
        {
            if (mlo is null) continue;
            foreach (var l in mlo.Lineas)
            {
                if (!lineaByModel.TryGetValue(l.ModelNo, out var lst))
                    lineaByModel[l.ModelNo] = lst = [];
                lst.Add(l);
            }
        }

        // Build pallet queue per model
        var queueByModel = new Dictionary<string, Queue<PalletSlot>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (modelNo, mloLineas) in lineaByModel)
        {
            int sp = spByModel.TryGetValue(modelNo, out var s) && s > 0 ? s : int.MaxValue;
            queueByModel[modelNo] = new Queue<PalletSlot>(BuildPallets(mloLineas, ownerByMloId, sp));
        }

        // Diagnostic: report what the MLO actually contains so mismatches are visible
        if (lineaByModel.Count > 0)
            advertencias.Insert(0, $"[MLO] Modelos en MLO: {string.Join(", ", lineaByModel.Keys.OrderBy(k => k))}");
        else
            advertencias.Insert(0, "[MLO] No se encontró ningún MLO asociado a estos FDOs.");

        // Map pallets to container positions
        // "piso": load all capa-1 positions across every fila first, then capa-2
        // "fila" (default): deepest fila first, capa is secondary
        var posiciones = modoOrden == "piso"
            ? plan.Posiciones
                .OrderBy(p => p.Capa).ThenBy(p => p.Fila).ThenBy(p => p.Lado)
                .ToList()
            : plan.Posiciones
                .OrderBy(p => p.Fila).ThenBy(p => p.Lado).ThenBy(p => p.Capa)
                .ToList();

        int orden = 1;
        foreach (var pos in posiciones)
        {
            if (!queueByModel.TryGetValue(pos.ModelNo, out var queue) || queue.Count == 0)
            {
                advertencias.Add($"Sin pallet MLO para {pos.ModelNo} (F{pos.Fila} {pos.Lado})");
                lineas.Add(new PickingLineaDto
                {
                    OrdenPicking   = orden++,
                    FilaContenedor = pos.Fila,
                    LadoContenedor = pos.Lado,
                    CapaContenedor = pos.Capa,
                    Consignee      = pos.Destino,
                    ModelNo        = pos.ModelNo,
                    Descripcion    = pos.Descripcion,
                    CaseNo         = "(sin MLO)",
                    FromLocation   = "(sin MLO)",
                    Qty            = pos.Piezas,
                    EsParcial      = true,
                });
                continue;
            }

            var slot = queue.Dequeue();
            lineas.Add(new PickingLineaDto
            {
                OrdenPicking     = orden++,
                FilaContenedor   = pos.Fila,
                LadoContenedor   = pos.Lado,
                CapaContenedor   = pos.Capa,
                Consignee        = pos.Destino,
                FdoSlipNo        = slot.FdoSlipNo,
                MloNo            = slot.MloNo,
                CaseNo           = slot.PrimaryCaseNo,
                FromLocation     = slot.PrimaryLocation,
                ModelNo          = pos.ModelNo,
                Descripcion      = pos.Descripcion,
                Qty                   = slot.Qty,
                EsParcial             = slot.EsParcial,
                NecesitaConfirmacion  = slot.NecesitaConfirmacion,
                Recomendacion         = slot.Recomendacion,
                CasesAdicionales      = slot.CasesAdicionales,
            });
        }

        return new PickingResultadoDto { Lineas = lineas, Advertencias = advertencias };
    }

    // ── pallet building ───────────────────────────────────────────────────────

    private static List<PalletSlot> BuildPallets(
        List<MloLinea> lineas,
        Dictionary<int, (Mlo Mlo, Fdo Fdo)> ownerByMloId,
        int sp)
    {
        // Sort CASEs individually by location proximity (Rack → Pos → Level)
        var sorted = lineas
            .OrderBy(l => MloXlsParser.ParseLocation(l.FromLocation).Rack)
            .ThenBy(l => MloXlsParser.ParseLocation(l.FromLocation).Pos)
            .ThenBy(l => MloXlsParser.ParseLocation(l.FromLocation).Level)
            .ToList();

        var pallets = new List<PalletSlot>();

        if (sp == int.MaxValue)
        {
            // No pallet size known — one entry per CASE
            foreach (var l in sorted)
            {
                GetOwner(l, ownerByMloId, out var fs, out var mn);
                pallets.Add(new PalletSlot(l.FromQty, false, false, null, l.CaseNo, l.FromLocation, fs, mn, []));
            }
            return pallets;
        }

        // Running accumulator for sub-sp remainders
        // Each entry: (linea, qty taken from it)
        var accum    = new List<(MloLinea Linea, int Qty)>();
        int accumQty = 0;

        void EmitAccum(bool isPartial)
        {
            if (accum.Count == 0) return;
            var primary = accum[0];
            GetOwner(primary.Linea, ownerByMloId, out var fs, out var mn);

            // Multi-ubicación: CASEs de distintos puntos del almacén → requiere staging
            bool multiLoc = accum.Select(x => x.Linea.FromLocation)
                                 .Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;

            // Descripción detallada para el operador (siempre que haya multi-loc)
            string? rec = multiLoc
                ? string.Join(" + ", accum.Select(x => $"{x.Linea.CaseNo} ({x.Qty} pzs @ {x.Linea.FromLocation})"))
                : null;

            pallets.Add(new PalletSlot(
                accumQty, isPartial, multiLoc, rec,
                primary.Linea.CaseNo, primary.Linea.FromLocation, fs, mn,
                accum.Count > 1 ? accum.Skip(1).Select(x => x.Linea.CaseNo).ToList() : []
            ));
            accum.Clear();
            accumQty = 0;
        }

        foreach (var linea in sorted)
        {
            GetOwner(linea, ownerByMloId, out var fdoSlip, out var mloNo);
            int rem = linea.FromQty;

            // Use this CASE to fill the current accumulator first
            if (accumQty > 0)
            {
                int needed = sp - accumQty;
                int take   = Math.Min(rem, needed);
                accum.Add((linea, take));
                accumQty += take;
                rem      -= take;
                if (accumQty == sp) EmitAccum(false); // pallet complete
            }

            // Emit full pallets from remaining qty of this single CASE — always single location
            while (rem >= sp)
            {
                pallets.Add(new PalletSlot(sp, false, false, null, linea.CaseNo, linea.FromLocation, fdoSlip, mloNo, []));
                rem -= sp;
            }

            // Any leftover starts (or continues) the accumulator
            if (rem > 0)
            {
                accum.Add((linea, rem));
                accumQty += rem;
            }
        }

        // Final partial
        if (accumQty > 0) EmitAccum(true);

        return pallets;
    }

    private static void GetOwner(MloLinea linea,
        Dictionary<int, (Mlo Mlo, Fdo Fdo)> map,
        out string fdoSlip, out string mloNo)
    {
        if (map.TryGetValue(linea.MloId, out var owner))
        {
            fdoSlip = owner.Fdo.FdoSlipNo;
            mloNo   = owner.Mlo.MloNo;
        }
        else { fdoSlip = ""; mloNo = ""; }
    }
}
