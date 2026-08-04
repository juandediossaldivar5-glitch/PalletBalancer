using PalletBalancer.Core.Models;

namespace PalletBalancer.Core.Services;

public interface ICalculadoraPeso
{
    double CalcularPesoPallet(Producto producto, int cantidadPiezas, double pesoPalletVacioKg);

    /// <summary>
    /// Construye un Pallet a partir del escaneo.
    /// Si piezasPorPalletCompleto > 0, escala AltoCm proporcionalmente:
    ///   AltoCm = Producto.AltoCm × (cantidadPiezas / piezasPorPalletCompleto)
    /// </summary>
    Pallet ProcesarEscaneo(string codigoEscaneado, Producto producto, int cantidadPiezas,
        double pesoPalletVacioKg = 22.0, int piezasPorPalletCompleto = 0);
}
