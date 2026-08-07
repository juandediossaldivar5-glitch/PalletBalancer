namespace PalletBalancer.Api.DTOs;

public class RecomendarDto
{
    public List<int> FdoIds { get; set; } = [];
}

public class RecomendacionItemDto
{
    public string Contenedor        { get; set; } = "";
    public string Tractocamion      { get; set; } = "";
    public bool   Viable            { get; set; }
    public double GVWKg             { get; set; }
    public double W1Kg              { get; set; }
    public double W2Kg              { get; set; }
    public double WrKg              { get; set; }
    public int    FilasUsadas       { get; set; }
    public int    FilasDisponibles  { get; set; }
    public double BalancePct        { get; set; }
    public List<string> Problemas   { get; set; } = [];
}
