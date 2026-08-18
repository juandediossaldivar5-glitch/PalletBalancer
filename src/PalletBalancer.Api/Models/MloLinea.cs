namespace PalletBalancer.Api.Models;

public class MloLinea
{
    public int Id { get; set; }
    public int MloId { get; set; }
    public Mlo Mlo { get; set; } = null!;

    public string ModelNo { get; set; } = string.Empty;  // col 0 — "FG Model" header
    public string SlipNo { get; set; } = string.Empty;  // col 1 — FDO ref
    public string Class { get; set; } = string.Empty;  // col 3
    public string CaseNo { get; set; } = string.Empty;  // col 5
    public string FromLocation { get; set; } = string.Empty;  // col 6 e.g. MEAX-FG1-56-17-02
    public int FromQty { get; set; }                  // col 7
    public string Check { get; set; } = string.Empty;  // col 10 "C" or ""
    public string Descripcion { get; set; } = string.Empty;  // col 15
    public string ToLocation { get; set; } = string.Empty;  // col 17 e.g. MEAX-FGVIA
}
