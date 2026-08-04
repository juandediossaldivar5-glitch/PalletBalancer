using System.ComponentModel.DataAnnotations;

namespace PalletBalancer.Api.Models;

public class Fdo
{
    public int Id { get; set; }

    [Required]
    public string FdoSlipNo { get; set; } = string.Empty;

    public DateOnly DsbDate   { get; set; }
    public DateOnly ShipDate  { get; set; }
    public string   Customer  { get; set; } = string.Empty;
    public string   Consignee { get; set; } = string.Empty;

    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;

    public List<FdoLinea> Lineas { get; set; } = [];
}
