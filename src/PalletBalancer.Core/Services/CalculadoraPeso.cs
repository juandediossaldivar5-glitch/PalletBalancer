using PalletBalancer.Core.Models;

namespace PalletBalancer.Core.Services;

public class CalculadoraPeso : ICalculadoraPeso
{
    public double CalcularPesoPallet(Producto producto, int cantidadPiezas, double pesoPalletVacioKg)
    {
        if (producto is null) throw new ArgumentNullException(nameof(producto));
        if (cantidadPiezas < 0) throw new ArgumentOutOfRangeException(nameof(cantidadPiezas), "La cantidad de piezas no puede ser negativa.");
        if (pesoPalletVacioKg < 0) throw new ArgumentOutOfRangeException(nameof(pesoPalletVacioKg));

        return Math.Round((producto.PesoUnitarioKg * cantidadPiezas) + pesoPalletVacioKg, 2);
    }

    public Pallet ProcesarEscaneo(string codigoEscaneado, Producto producto, int cantidadPiezas,
        double pesoPalletVacioKg = 22.0, int piezasPorPalletCompleto = 0)
    {
        if (string.IsNullOrWhiteSpace(codigoEscaneado))
            throw new ArgumentException("El código escaneado no puede estar vacío.", nameof(codigoEscaneado));

        double altoCm = piezasPorPalletCompleto > 0 && cantidadPiezas > 0
            ? Math.Round(producto.AltoCm * (double)cantidadPiezas / piezasPorPalletCompleto, 1)
            : producto.AltoCm;

        return new Pallet
        {
            CodigoEscaneado = codigoEscaneado,
            Sku = producto.Sku,
            CantidadPiezas = cantidadPiezas,
            PesoPalletVacioKg = pesoPalletVacioKg,
            PesoTotalKg = CalcularPesoPallet(producto, cantidadPiezas, pesoPalletVacioKg),
            LargoCm = producto.LargoCm > 0 ? producto.LargoCm : 120,
            AnchoCm = producto.AnchoCm > 0 ? producto.AnchoCm : 100,
            AltoCm = altoCm
        };
    }
}
