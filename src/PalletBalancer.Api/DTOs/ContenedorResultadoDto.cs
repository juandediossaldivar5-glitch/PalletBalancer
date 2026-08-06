namespace PalletBalancer.Api.DTOs;

public class ContenedorResultadoDto
{
    public List<PosicionResultadoDto> Posiciones      { get; set; } = [];
    public List<DestinoInfoDto>       Destinos        { get; set; } = [];
    public double  PesoIzquierdoKg                   { get; set; }
    public double  PesoDerechoKg                     { get; set; }
    public double  PesoTotalKg                       { get; set; }
    public double  DiferenciaPorcentual               { get; set; }
    public bool    DentroDeTolerancia                { get; set; }
    public List<string> Advertencias                { get; set; } = [];
    public int     TotalPallets                     { get; set; }
    public List<string> ModelosSinDatos             { get; set; } = [];
}

public class PosicionResultadoDto
{
    public int    Fila        { get; set; }
    public string Lado        { get; set; } = "";
    public string ModelNo     { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Destino     { get; set; } = "";
    public double PesoKg      { get; set; }
    public int    Piezas      { get; set; }
    public int    Capas       { get; set; }
}

public class DestinoInfoDto
{
    public string Consignee     { get; set; } = "";
    public int    OrdenDescarga { get; set; } // 1 = primero en descargar (puertas)
    public int    TotalPallets  { get; set; }
    public int    FilaInicio    { get; set; }
    public int    FilaFin       { get; set; }
}
