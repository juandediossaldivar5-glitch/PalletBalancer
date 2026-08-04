namespace PalletBalancer.Core.Models;

public class Pallet
{
    public string CodigoEscaneado { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int CantidadPiezas { get; set; }
    public double PesoPalletVacioKg { get; set; } = 22.0;
    public double PesoTotalKg { get; set; }
    public double LargoCm { get; set; } = 120;
    public double AnchoCm { get; set; } = 100;
    public double AltoCm { get; set; }
}
