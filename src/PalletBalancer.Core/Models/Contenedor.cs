namespace PalletBalancer.Core.Models;

public class Contenedor
{
    public string Tipo { get; set; } = "53ft";
    public double LargoCm { get; set; } = 1600;
    public double AnchoCm { get; set; } = 260;
    public double AltoCm { get; set; } = 250;
    public double CapacidadMaximaKg { get; set; } = 22000;

    /// <summary>Posiciones de pallet disponibles por lado (izquierdo y derecho tienen la misma cantidad).</summary>
    public int FilasDisponibles { get; set; } = 26;
}
