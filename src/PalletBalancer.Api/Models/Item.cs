using System.ComponentModel.DataAnnotations;

namespace PalletBalancer.Api.Models;

public class Item
{
    [Key]
    public string ModelNo    { get; set; } = string.Empty;
    [Required]
    public string Descripcion { get; set; } = string.Empty;

    // Standard Pack (pallet completo)
    public int    SpPiezasPorPallet { get; set; }
    public double SpPesoKg          { get; set; }
    public double SpLargoCm         { get; set; }
    public double SpAnchoCm         { get; set; }
    public double SpAltoCm          { get; set; }

    // Caja (unidad intermedia)
    public int    CajaPiezasPorCaja { get; set; }
    public double CajaPesoKg        { get; set; }
    public double CajaLargoCm       { get; set; }
    public double CajaAnchoCm       { get; set; }
    public double CajaAltoCm        { get; set; }

    // Pieza (unidad mínima)
    public double PiezaPesoKg  { get; set; }
    public double PiezaLargoCm { get; set; }
    public double PiezaAnchoCm { get; set; }
    public double PiezaAltoCm  { get; set; }
}
