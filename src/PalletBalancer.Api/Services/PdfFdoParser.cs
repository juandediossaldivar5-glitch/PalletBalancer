using UglyToad.PdfPig;
using PalletBalancer.Api.DTOs;

namespace PalletBalancer.Api.Services;

public static class PdfFdoParser
{
    public static FdoImportadoDto Parsear(Stream pdfStream)
    {
        var lineas = ExtraerLineas(pdfStream);
        return ParsearLineas(lineas);
    }

    // Internal parsing — public for testability
    public static FdoImportadoDto ParsearLineas(IReadOnlyList<string> lineas)
    {
        var dto = new FdoImportadoDto();

        foreach (var linea in lineas)
        {
            if (TryExtraerValor(linea, "FDO Slip No",       out var v)) dto.FdoSlipNo = v;
            else if (TryExtraerValor(linea, "Disbursement Date", out v)) dto.DsbDate   = NormalizarFecha(v);
            else if (TryExtraerValor(linea, "Ship Date",         out v)) dto.ShipDate  = NormalizarFecha(v);
            else if (TryExtraerValor(linea, "Customer",          out v)) dto.Customer  = v;
            else if (TryExtraerValor(linea, "Consignee",         out v)) dto.Consignee = v;
        }

        dto.Lineas = ParsearLineasProducto(lineas);
        return dto;
    }

    private static List<string> ExtraerLineas(Stream pdfStream)
    {
        using var pdf   = PdfDocument.Open(pdfStream);
        var       todas = new List<string>();

        foreach (var pagina in pdf.GetPages())
        {
            var porFila = pagina.GetWords()
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
                .OrderByDescending(g => g.Key)
                .Select(g => string.Join(" ",
                    g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
            todas.AddRange(porFila);
        }
        return todas;
    }

    private static bool TryExtraerValor(string linea, string etiqueta, out string valor)
    {
        var idx = linea.IndexOf(etiqueta, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) { valor = ""; return false; }
        valor = linea[(idx + etiqueta.Length)..].TrimStart(':', ' ').Trim();
        return !string.IsNullOrWhiteSpace(valor);
    }

    private static string NormalizarFecha(string raw)
    {
        if (DateTime.TryParse(raw, out var dt))
            return dt.ToString("yyyy-MM-dd");
        return raw;
    }

    private static List<FdoLineaDto> ParsearLineasProducto(IReadOnlyList<string> lineas)
    {
        var resultado = new List<FdoLineaDto>();

        foreach (var linea in lineas)
        {
            var partes = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Línea de producto: mínimo 3 tokens, segundo token parece código de modelo
            // (empieza con letra, ≥8 chars), último token es número entero
            if (partes.Length >= 3
                && partes[1].Length >= 6
                && char.IsLetter(partes[1][0])
                && int.TryParse(partes[^1], out var qty)
                && qty > 0)
            {
                resultado.Add(new FdoLineaDto
                {
                    CustomerPoNo = partes[0],
                    ModelNo      = partes[1],
                    ReqQty       = qty
                });
            }
        }
        return resultado;
    }
}
