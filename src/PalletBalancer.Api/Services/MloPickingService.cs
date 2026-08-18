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

    // Internal entry: a CASE with its effective qty (may be remainder after full pallets)
    private sealed record CaseEntry(MloLinea Linea, int Qty);

    private static List<PalletSlot> BuildPallets(
        List<MloLinea> lineas,
        Dictionary<int, (Mlo Mlo, Fdo Fdo)> ownerByMloId,
        int sp)
    {
        // Sort CASEs by location proximity (Rack → Pos → Level)
        // Same-location entries stay adjacent → FindExactSum tries them first → implicit location preference
        var sorted = lineas
            .OrderBy(l => MloXlsParser.ParseLocation(l.FromLocation).Rack)
            .ThenBy(l => MloXlsParser.ParseLocation(l.FromLocation).Pos)
            .ThenBy(l => MloXlsParser.ParseLocation(l.FromLocation).Level)
            .ToList();

        var pallets = new List<PalletSlot>();

        if (sp == int.MaxValue)
        {
            foreach (var l in sorted)
            {
                GetOwner(l, ownerByMloId, out var fs, out var mn);
                pallets.Add(new PalletSlot(l.FromQty, false, false, null, l.CaseNo, l.FromLocation, fs, mn, []));
            }
            return pallets;
        }

        // Phase 1 — full pallets: each CASE emits floor(qty/sp) complete pallets.
        // Remainders (qty % sp) go to phase 2 as CaseEntry with reduced qty.
        var subsp = new List<CaseEntry>();
        foreach (var l in sorted)
        {
            GetOwner(l, ownerByMloId, out var fdoSlip, out var mloNo);
            int rem = l.FromQty;
            while (rem >= sp)
            {
                pallets.Add(new PalletSlot(sp, false, false, null, l.CaseNo, l.FromLocation, fdoSlip, mloNo, []));
                rem -= sp;
            }
            if (rem > 0) subsp.Add(new CaseEntry(l, rem));
        }

        // Phase 2 — exact-sum matching: combine sub-sp entries into pallets of exactly sp
        // WITHOUT splitting any CASE. Uses backtracking to find valid subsets.
        // Entries sorted by location proximity means same-location partners are tried first.
        var used = new bool[subsp.Count];

        for (int i = 0; i < subsp.Count; i++)
        {
            if (used[i]) continue;
            var indices = FindExactSum(subsp, used, i + 1, sp - subsp[i].Qty);
            if (indices != null)
            {
                used[i] = true;
                foreach (var ci in indices) used[ci] = true;
                var group = new[] { i }.Concat(indices).Select(ci => subsp[ci]).ToList();
                pallets.Add(MakeGroupPallet(group, sp, false, ownerByMloId));
            }
        }

        // Phase 3 — fallback linear accumulation for entries with no exact partner.
        // Cases MAY be split here as a last resort; clearly signals as CONFIRMAR.
        var leftover = subsp.Where((_, i) => !used[i]).ToList();
        var accum    = new List<CaseEntry>();
        int accumQty = 0;

        foreach (var entry in leftover)
        {
            int rem = entry.Qty;

            if (accumQty > 0)
            {
                int take = Math.Min(rem, sp - accumQty);
                accum.Add(new CaseEntry(entry.Linea, take));
                accumQty += take;
                rem      -= take;
                if (accumQty == sp)
                {
                    pallets.Add(MakeGroupPallet(accum, sp, false, ownerByMloId));
                    accum    = [];
                    accumQty = 0;
                }
            }

            while (rem >= sp)
            {
                GetOwner(entry.Linea, ownerByMloId, out var fs, out var mn);
                pallets.Add(new PalletSlot(sp, false, false, null, entry.Linea.CaseNo, entry.Linea.FromLocation, fs, mn, []));
                rem -= sp;
            }

            if (rem > 0)
            {
                accum.Add(new CaseEntry(entry.Linea, rem));
                accumQty += rem;
            }
        }

        if (accumQty > 0)
            pallets.Add(MakeGroupPallet(accum, accumQty, true, ownerByMloId));

        return pallets;
    }

    // Backtracking subset-sum: finds indices in `entries` (from `start` onward, skipping `used`)
    // whose Qty values sum exactly to `target`. Returns null if no solution exists.
    // Does NOT modify `used` permanently — caller marks indices on success.
    private static List<int>? FindExactSum(List<CaseEntry> entries, bool[] used, int start, int target)
    {
        if (target == 0) return [];
        for (int i = start; i < entries.Count; i++)
        {
            if (used[i] || entries[i].Qty > target) continue;
            used[i] = true;
            var rest = FindExactSum(entries, used, i + 1, target - entries[i].Qty);
            used[i] = false; // restore for backtracking
            if (rest != null) return [i, ..rest];
        }
        return null;
    }

    private static PalletSlot MakeGroupPallet(
        List<CaseEntry> group, int qty, bool isPartial,
        Dictionary<int, (Mlo Mlo, Fdo Fdo)> ownerByMloId)
    {
        var primary = group[0];
        GetOwner(primary.Linea, ownerByMloId, out var fdoSlip, out var mloNo);
        bool multiLoc = group.Select(e => e.Linea.FromLocation)
                             .Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;
        string? rec = multiLoc
            ? string.Join(" + ", group.Select(e => $"{e.Linea.CaseNo} ({e.Qty} pzs @ {e.Linea.FromLocation})"))
            : null;
        return new PalletSlot(
            qty, isPartial, multiLoc, rec,
            primary.Linea.CaseNo, primary.Linea.FromLocation, fdoSlip, mloNo,
            group.Count > 1 ? group.Skip(1).Select(e => e.Linea.CaseNo).ToList() : []
        );
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
