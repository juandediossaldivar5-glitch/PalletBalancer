namespace PalletBalancer.Api.DTOs;

public class CalcularContenedorDto
{
    public List<int>     FdoIds        { get; set; } = [];
    public List<string>? OrdenDescarga { get; set; } // null = orden automático
}
