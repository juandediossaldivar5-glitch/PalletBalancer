using PalletBalancer.Api.Services;
using Xunit;

namespace PalletBalancer.Core.Tests;

public class PdfFdoParserTests
{
    [Fact]
    public void ParsearLineas_ExtraeCamposDeEncabezado()
    {
        var lineas = new[]
        {
            "FDO Slip No: 2612481",
            "Disbursement Date: 2026-08-03",
            "Ship Date: 2026-08-15",
            "Customer: MD LOGIS SA",
            "Consignee: MITSUBISHI MOTORS"
        };

        var dto = PdfFdoParser.ParsearLineas(lineas);

        Assert.Equal("2612481",          dto.FdoSlipNo);
        Assert.Equal("2026-08-03",       dto.DsbDate);
        Assert.Equal("2026-08-15",       dto.ShipDate);
        Assert.Equal("MD LOGIS SA",      dto.Customer);
        Assert.Equal("MITSUBISHI MOTORS", dto.Consignee);
    }

    [Fact]
    public void ParsearLineas_ExtraeLineasProducto()
    {
        var lineas = new[]
        {
            "PO-001 K006T91071XB 120",
            "PO-002 K006T91072XB 240"
        };

        var dto = PdfFdoParser.ParsearLineas(lineas);

        Assert.Equal(2, dto.Lineas.Count);
        Assert.Equal("PO-001",        dto.Lineas[0].CustomerPoNo);
        Assert.Equal("K006T91071XB",  dto.Lineas[0].ModelNo);
        Assert.Equal(120,             dto.Lineas[0].ReqQty);
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
