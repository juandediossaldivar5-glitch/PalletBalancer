namespace PalletBalancer.Api.DTOs;

public class ContenedorResultadoDto
{
    public List<PosicionResultadoDto> Posiciones   { get; set; } = [];
    public double  PesoIzquierdoKg                 { get; set; }
    public double  PesoDerechoKg                   { get; set; }
    public double  PesoTotalKg                     { get; set; }
    public double  DiferenciaPorcentual             { get; set; }
    public bool    DentroDeTolerancia              { get; set; }
    public List<string> Advertencias              { get; set; } = [];
    public int     TotalPallets                   { get; set; }
    public List<string> ModelosSinDatos           { get; set; } = [];
}

public class PosicionResultadoDto
{
    public int    Fila        { get; set; }
    public string Lado        { get; set; } = "";
    public string ModelNo     { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public double PesoKg      { get; set; }
    public int    Piezas      { get; set; }
    public int    Capas       { get; set; }
}
