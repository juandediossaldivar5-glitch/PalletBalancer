namespace PalletBalancer.Api.DTOs;

public class CalcularContenedorDto
{
    public List<int>     FdoIds          { get; set; } = [];
    public List<string>? OrdenDescarga   { get; set; }
    public string?       TipoContenedor   { get; set; } // "20ft","40ft","40ft HC","45ft HC","53ft"
    public string?       TipoTractocamion { get; set; } // "T3-S2 Estándar","T3-S2 Cabina Larga","T3-S2 Day Cab"
}
