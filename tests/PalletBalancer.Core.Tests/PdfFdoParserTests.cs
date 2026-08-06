using PalletBalancer.Api.Services;
using Xunit;

namespace PalletBalancer.Core.Tests;

public class PdfFdoParserTests
{
    [Fact]
    public void ParsearLineas_ExtraeCamposDeEncabezado()
    {
        // Formato real del PDF MD Logis: varios labels en la misma línea
        var lineas = new[]
        {
            "FDO Slip No. : 2612481 Dsb. Date : 2026/08/03 Ship Date : 2026/08/03 Reason : FDO01 For Delivery (Normal)",
            "Customer : FORD CEP Mitsubishi Electric Automotive America, Inc.",
            "Consignee : FORD CE Ford Cleveland Engine Plant 1"
        };

        var dto = PdfFdoParser.ParsearLineas(lineas);

        Assert.Equal("2612481",    dto.FdoSlipNo);
        Assert.Equal("2026-08-03", dto.DsbDate);
        Assert.Equal("2026-08-03", dto.ShipDate);
        Assert.Equal("FORD CEP Mitsubishi Electric Automotive America, Inc.", dto.Customer);
        Assert.Equal("FORD CE Ford Cleveland Engine Plant 1", dto.Consignee);
    }

    [Fact]
    public void ParsearLineas_ExtraeLineasProducto()
    {
        // Formato real: línea de modelo + línea de cantidades con "PC" al final
        var lineas = new[]
        {
            "5700173375 2612481 P2GE 6C524 AB K006T91071XB 902 4G-VCT MPC",
            "6,648 6,648 0 1,152 0 0 1,152 PC",
            "5700173353 2612481 P2GE 6C525 AB K006T91072XB 902 FORD 4G-VVT MPC",
            "6,972 6,972 0 1,728 0 0 1,728 PC"
        };

        var dto = PdfFdoParser.ParsearLineas(lineas);

        Assert.Equal(2,              dto.Lineas.Count);
        Assert.Equal("5700173375",   dto.Lineas[0].CustomerPoNo);
        Assert.Equal("K006T91071XB", dto.Lineas[0].ModelNo);
        Assert.Equal(1152,           dto.Lineas[0].ReqQty);
        Assert.Equal("5700173353",   dto.Lineas[1].CustomerPoNo);
        Assert.Equal("K006T91072XB", dto.Lineas[1].ModelNo);
        Assert.Equal(1728,           dto.Lineas[1].ReqQty);
    }

    [Fact]
    public void ParsearLineas_CamposAusentes_RetornaStringVacio()
    {
        var lineas = new[] { "Texto irrelevante sin campos conocidos" };

        var dto = PdfFdoParser.ParsearLineas(lineas);

        Assert.Equal("", dto.FdoSlipNo);
        Assert.Empty(dto.Lineas);
    }
}
