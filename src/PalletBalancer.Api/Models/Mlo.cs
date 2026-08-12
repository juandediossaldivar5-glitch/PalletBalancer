namespace PalletBalancer.Api.Models;

public class Mlo
{
    public int Id { get; set; }
    public string MloNo { get; set; } = string.Empty;  // e.g. "MLO00302753"
    public int FdoId { get; set; }
    public Fdo Fdo { get; set; } = null!;
    public DateOnly FechaEntrega { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public List<MloLinea> Lineas { get; set; } = [];
}
