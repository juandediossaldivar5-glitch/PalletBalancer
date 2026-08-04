namespace PalletBalancer.Core.Models;

public class ConfiguracionCarga
{
    public Contenedor Contenedor { get; set; } = new();
    public ConfigPalletEstandar PalletEstandar { get; set; } = new();
    public ConfigReglas Reglas { get; set; } = new();
}

public class ConfigPalletEstandar
{
    public double LargoCm { get; set; } = 120;
    public double AnchoCm { get; set; } = 120;
    public double AltoCm { get; set; } = 120;
    public double PesoPalletVacioKg { get; set; } = 22.0;

    /// <summary>
    /// Cantidad de piezas que forman un pallet completo.
    /// 0 = apilado deshabilitado (cada unidad ocupa su propia posición).
    /// </summary>
    public int PiezasPorPalletCompleto { get; set; } = 0;
}

public class ConfigReglas
{
    public double ToleranciaBalanceoPorc { get; set; } = 5.0;
}
