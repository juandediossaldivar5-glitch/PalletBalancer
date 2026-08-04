namespace PalletBalancer.Core.Models;

public class PosicionPallet
{
    /// <summary>
    /// Capas apiladas de abajo hacia arriba: índice 0 = base (más pesada), último = tope.
    /// Sin apilado configurado siempre tiene exactamente 1 elemento.
    /// </summary>
    public List<Pallet> Capas { get; set; } = new();

    public LadoContenedor Lado { get; set; }

    /// <summary>Fila 1-based, contada desde la cabina (1) hacia las puertas.</summary>
    public int Fila { get; set; }

    public double PesoAcumuladoLadoKg { get; set; }

    public double PesoTotalKg => Capas.Sum(c => c.PesoTotalKg);
    public double AlturaStackCm => Capas.Sum(c => c.AltoCm);

    /// <summary>Acceso directo a la capa base. Compatible con código que no usa apilado.</summary>
    public Pallet Pallet => Capas[0];
}
