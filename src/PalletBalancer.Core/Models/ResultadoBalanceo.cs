namespace PalletBalancer.Core.Models;

public class ResultadoBalanceo
{
    public List<PosicionPallet> Posiciones { get; set; } = new();
    public double PesoTotalKg { get; set; }
    public double PesoLadoIzquierdoKg { get; set; }
    public double PesoLadoDerechoKg { get; set; }
    public double DiferenciaPorcentual { get; set; }
    public bool DentroDeTolerancia { get; set; }
    public List<string> Advertencias { get; set; } = new();
}
