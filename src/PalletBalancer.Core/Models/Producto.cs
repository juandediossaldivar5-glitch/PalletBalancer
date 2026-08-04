namespace PalletBalancer.Core.Models;

/// <summary>
/// Catálogo maestro: peso y medidas por SKU. Es la fuente que permite
/// calcular el peso de un pallet a partir de la cantidad de piezas escaneadas.
/// </summary>
public class Producto
{
    public string Sku { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public double PesoUnitarioKg { get; set; }
    public double LargoCm { get; set; }
    public double AnchoCm { get; set; }
    public double AltoCm { get; set; }
}
