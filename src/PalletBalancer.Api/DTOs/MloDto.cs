namespace PalletBalancer.Api.DTOs;

public class MloResumenDto
{
    public int    Id          { get; set; }
    public string MloNo       { get; set; } = "";
    public int    FdoId       { get; set; }
    public int    TotalLineas { get; set; }
}

public class MloDto
{
    public int    Id        { get; set; }
    public string MloNo     { get; set; } = "";
    public int    FdoId     { get; set; }
    public string FdoSlipNo { get; set; } = "";
    public List<MloLineaDto> Lineas { get; set; } = [];
}

public class MloLineaDto
{
    public int    Id           { get; set; }
    public string CaseNo       { get; set; } = "";
    public string ModelNo      { get; set; } = "";
    public string Descripcion  { get; set; } = "";
    public string FromLocation { get; set; } = "";
    public int    FromQty      { get; set; }
    public string Check        { get; set; } = "";
}

// Request for picking endpoint
public class PickingRequestDto
{
    public List<int>     FdoIds           { get; set; } = [];
    public string?       TipoContenedor   { get; set; }
    public string?       TipoTractocamion { get; set; }
    public List<string>? OrdenDescarga    { get; set; }
}

// One line in the picking list
public class PickingLineaDto
{
    public int    OrdenPicking    { get; set; }  // 1-based, ascending = pick first
    public int    FilaContenedor  { get; set; }
    public string LadoContenedor  { get; set; } = "";
    public int    CapaContenedor  { get; set; }
    public string Consignee       { get; set; } = "";
    public string FdoSlipNo       { get; set; } = "";
    public string MloNo           { get; set; } = "";
    public string CaseNo          { get; set; } = "";
    public string FromLocation    { get; set; } = "";
    public string ModelNo         { get; set; } = "";
    public string Descripcion     { get; set; } = "";
    public int    Qty             { get; set; }
    public bool   EsParcial       { get; set; }
}

public class PickingResultadoDto
{
    public List<PickingLineaDto> Lineas     { get; set; } = [];
    public List<string>          Advertencias { get; set; } = [];
}
