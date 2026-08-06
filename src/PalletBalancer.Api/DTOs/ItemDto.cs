namespace PalletBalancer.Api.DTOs;

public class ItemDto
{
    public string ModelNo     { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    public int    SpPiezasPorPallet { get; set; }
    public double SpPesoKg          { get; set; }
    public double SpLargoCm         { get; set; }
    public double SpAnchoCm         { get; set; }
    public double SpAltoCm          { get; set; }
    public bool   PuedeEstibar      { get; set; }

    public int    CajaPiezasPorCaja { get; set; }
    public double CajaPesoKg        { get; set; }
    public double CajaLargoCm       { get; set; }
    public double CajaAnchoCm       { get; set; }
    public double CajaAltoCm        { get; set; }

    public double PiezaPesoKg  { get; set; }
    public double PiezaLargoCm { get; set; }
    public double PiezaAnchoCm { get; set; }
    public double PiezaAltoCm  { get; set; }
}
