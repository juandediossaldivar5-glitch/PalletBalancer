using PalletBalancer.Api.DTOs;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Services;

public static class MloPickingService
{
    // ── internal types ────────────────────────────────────────────────────────

    private sealed record LocGroup(
        string Loc,
        (int R, int P, int L) SortKey,
        List<MloLinea> Lines)
    {
        public int          TotalQty => Lines.Sum(l => l.FromQty);
        public List<string> CaseNos  => Lines.Select(l => l.CaseNo).ToList();
    }

    private sealed record PalletSlot(
        int          Qty,
        bool         EsParcial,
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
                Qty              = slot.Qty,
                EsParcial        = slot.EsParcial,
                Recomendacion    = slot.Recomendacion,
                CasesAdicionales = slot.CasesAdicionales,
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
        // Step 1: group CASEs by exact location, sort by proximity (Rack → Pos → Level)
        var groups = lineas
            .GroupBy(l => l.FromLocation)
            .Select(g => new LocGroup(g.Key, MloXlsParser.ParseLocation(g.Key), g.ToList()))
            .OrderBy(g => g.SortKey.R)
            .ThenBy(g => g.SortKey.P)
            .ThenBy(g => g.SortKey.L)
            .ToList();

        var pallets = new List<PalletSlot>();

        // Step 2: accumulate location groups into pallet "attempts".
        // An attempt collects groups until the total qty ≥ sp, then flushes:
        //   · complete pallets  = floor(totalQty / sp)
        //   · remainder         = totalQty % sp   → partial pallet
        //
        // Same-location groups are already one logical unit (always complete if
        // their total is a multiple of sp).  No recommendation is added for
        // single-location partials.  A recommendation IS added when the partial
        // results from combining groups across different locations.

        var attempt    = new List<LocGroup>();
        int attemptQty = 0;

        void Flush()
        {
            if (attemptQty == 0) return;

            bool multiLoc    = attempt.Count > 1;
            int  nComplete   = sp == int.MaxValue ? 1              : attemptQty / sp;
            int  remainder   = sp == int.MaxValue ? 0              : attemptQty % sp;
            int  completeQty = sp == int.MaxValue ? attemptQty     : sp;

            // All case nos across the attempt (for CasesAdicionales on complete pallets)
            var allCaseNos = attempt.SelectMany(g => g.CaseNos).ToList();

            // Emit complete pallets — attribute to first group for location display
            var first = attempt[0];
            GetOwner(first, ownerByMloId, out var fdoSlip, out var mloNo);

            for (int k = 0; k < nComplete; k++)
            {
                pallets.Add(new PalletSlot(
                    completeQty, false, null,
                    first.CaseNos[0], first.Loc,
                    fdoSlip, mloNo,
                    allCaseNos.Count > 1 ? allCaseNos.Skip(1).ToList() : []
                ));
            }

            // Emit partial pallet (remainder)
            int partialQty = remainder > 0 ? remainder
                           : nComplete == 0 ? attemptQty   // all qty < sp
                           : 0;

            if (partialQty > 0)
            {
                var last = attempt.Last();
                GetOwner(last, ownerByMloId, out var fdoSlip2, out var mloNo2);
                string? rec = multiLoc ? BuildRec(attempt, nComplete, completeQty, partialQty) : null;

                pallets.Add(new PalletSlot(
                    partialQty, true, rec,
                    last.CaseNos[0], last.Loc,
                    fdoSlip2, mloNo2, []
                ));
            }

            attempt.Clear();
            attemptQty = 0;
        }

        foreach (var group in groups)
        {
            attempt.Add(group);
            attemptQty += group.TotalQty;

            // Same-location groups are always kept together — only flush when we
            // have accumulated enough for at least one complete pallet.
            if (sp != int.MaxValue && attemptQty >= sp)
                Flush();
        }

        Flush(); // remaining partial (if qty < sp for all accumulated groups)

        return pallets;
    }

    private static void GetOwner(LocGroup g,
        Dictionary<int, (Mlo Mlo, Fdo Fdo)> map,
        out string fdoSlip, out string mloNo)
    {
        if (g.Lines.Count > 0 && map.TryGetValue(g.Lines[0].MloId, out var owner))
        {
            fdoSlip = owner.Fdo.FdoSlipNo;
            mloNo   = owner.Mlo.MloNo;
        }
        else { fdoSlip = ""; mloNo = ""; }
    }

    private static string BuildRec(
        List<LocGroup> groups, int nComplete, int sp, int remainder)
    {
        var parts = groups.Select(g =>
            $"{string.Join("+", g.CaseNos)} ({g.TotalQty} pzs @ {g.Loc})");
        var sb = new System.Text.StringBuilder("Recomendación: unir ");
        sb.Append(string.Join(" + ", parts));
        sb.Append(" en área de staging.");
        if (nComplete > 0)
            sb.Append($" Resultado: {nComplete} pallet(s) completo(s) de {sp} pzs +");
        sb.Append($" {remainder} pzs sobrante (este parcial).");
        return sb.ToString();
    }
}
