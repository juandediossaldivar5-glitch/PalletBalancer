namespace PalletBalancer.Api.DTOs;

public class FdoDto
{
    public string FdoSlipNo  { get; set; } = string.Empty;
    public string DsbDate    { get; set; } = string.Empty;  // "2026-08-03"
    public string ShipDate   { get; set; } = string.Empty;
    public string Customer   { get; set; } = string.Empty;
    public string Consignee  { get; set; } = string.Empty;
    public List<FdoLineaDto> Lineas { get; set; } = [];
}

public class FdoLineaDto
{
    public string CustomerPoNo { get; set; } = string.Empty;
    public string ModelNo      { get; set; } = string.Empty;
    public string Descripcion  { get; set; } = string.Empty;
    public int    ReqQty       { get; set; }
}
