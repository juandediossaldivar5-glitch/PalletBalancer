using PalletBalancer.Core.Models;
using PalletBalancer.Core.Services;
using Xunit;

namespace PalletBalancer.Core.Tests;

public class CalculadoraPesoTests
{
    private readonly ICalculadoraPeso _calculadora = new CalculadoraPeso();

    [Fact]
    public void CalcularPesoPallet_SumaPiezasMasPesoVacio()
    {
        var producto = new Producto { Sku = "A1", PesoUnitarioKg = 2.5 };

        double peso = _calculadora.CalcularPesoPallet(producto, 100, 22);

        Assert.Equal(272, peso);
    }

    [Fact]
    public void CalcularPesoPallet_CantidadCero_SoloRetornaPesoVacio()
    {
        var producto = new Producto { Sku = "A1", PesoUnitarioKg = 5 };

        double peso = _calculadora.CalcularPesoPallet(producto, 0, 20);

        Assert.Equal(20, peso);
    }

    [Fact]
    public void CalcularPesoPallet_CantidadNegativa_LanzaExcepcion()
    {
        var producto = new Producto { Sku = "A1", PesoUnitarioKg = 5 };

        Assert.Throws<ArgumentOutOfRangeException>(() => _calculadora.CalcularPesoPallet(producto, -1, 20));
    }

    [Fact]
    public void ProcesarEscaneo_GeneraPalletConPesoCalculado()
    {
        var producto = new Producto { Sku = "B2", PesoUnitarioKg = 1.2, LargoCm = 120, AnchoCm = 100 };

        var pallet = _calculadora.ProcesarEscaneo("QR-001", producto, 50);

        Assert.Equal("QR-001", pallet.CodigoEscaneado);
        Assert.Equal("B2", pallet.Sku);
        Assert.Equal(50, pallet.CantidadPiezas);
        Assert.Equal(82, pallet.PesoTotalKg); // 1.2*50 + 22
    }

    [Fact]
    public void ProcesarEscaneo_CodigoVacio_LanzaExcepcion()
    {
        var producto = new Producto { Sku = "B2", PesoUnitarioKg = 1.2 };

        Assert.Throws<ArgumentException>(() => _calculadora.ProcesarEscaneo("", producto, 10));
    }
}
