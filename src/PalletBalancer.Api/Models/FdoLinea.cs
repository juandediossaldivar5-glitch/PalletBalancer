namespace PalletBalancer.Api.Models;

public class FdoLinea
{
    public int Id { get; set; }

    public int FdoId { get; set; }
    public Fdo Fdo   { get; set; } = null!;

    public string CustomerPoNo { get; set; } = string.Empty;
    public string ModelNo      { get; set; } = string.Empty;
    public Item?  Item         { get; set; }
    public int    ReqQty       { get; set; }
}
