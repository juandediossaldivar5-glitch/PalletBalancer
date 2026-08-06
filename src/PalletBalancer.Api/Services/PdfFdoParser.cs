using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using PalletBalancer.Api.DTOs;

namespace PalletBalancer.Api.Services;

public static class PdfFdoParser
{
    // K006T91071XB — letra + 3 dígitos + letra + 5 dígitos + 2 letras
    private static readonly Regex ModeloRegex = new(@"\b[A-Z]\d{3}[A-Z]\d{5}[A-Z]{2}\b");

    public static FdoImportadoDto Parsear(Stream pdfStream)
    {
        var lineas = ExtraerLineas(pdfStream);
        return ParsearLineas(lineas);
    }

    public static FdoImportadoDto ParsearLineas(IReadOnlyList<string> lineas)
    {
        var dto = new FdoImportadoDto();

        foreach (var linea in lineas)
        {
            // Cada campo se evalúa independientemente (no else-if) porque
            // varios labels pueden aparecer en la misma línea del PDF.

            if (string.IsNullOrEmpty(dto.FdoSlipNo)
                && TryExtraerValor(linea, "FDO Slip No.", out var v))
                dto.FdoSlipNo = v.Split(' ')[0].Trim();

            if (string.IsNullOrEmpty(dto.DsbDate)
                && TryExtraerValor(linea, "Dsb. Date", out v))
                dto.DsbDate = NormalizarFecha(v.Split(' ')[0]);

            if (string.IsNullOrEmpty(dto.ShipDate)
                && TryExtraerValor(linea, "Ship Date", out v))
                dto.ShipDate = NormalizarFecha(v.Split(' ')[0]);

            // Evitar que "Customer PO No." o "Customer Model" disparen este campo
            if (string.IsNullOrEmpty(dto.Customer)
                && !linea.Contains("PO",    StringComparison.OrdinalIgnoreCase)
                && !linea.Contains("Model", StringComparison.OrdinalIgnoreCase)
                && TryExtraerValor(linea, "Customer", out v))
                dto.Customer = v.Trim();

            if (string.IsNullOrEmpty(dto.Consignee)
                && TryExtraerValor(linea, "Consignee", out v))
                dto.Consignee = v.Trim();
        }

        dto.Lineas = ParsearLineasProducto(lineas);
        return dto;
    }

    public static List<string> ExtraerLineasPublico(Stream pdfStream) => ExtraerLineas(pdfStream);

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
        valor = linea[(idx + etiqueta.Length)..].TrimStart(':', '.', ' ').Trim();
        return !string.IsNullOrWhiteSpace(valor);
    }

    private static string NormalizarFecha(string raw)
    {
        // Soporta: 2026/08/03, 2026-08-03, 08/03/2026
        if (DateTime.TryParse(raw, out var dt))
            return dt.ToString("yyyy-MM-dd");
        return raw;
    }

    private static List<FdoLineaDto> ParsearLineasProducto(IReadOnlyList<string> lineas)
    {
        var resultado  = new List<FdoLineaDto>();
        FdoLineaDto? pendiente = null;

        foreach (var linea in lineas)
        {
            var m = ModeloRegex.Match(linea);
            if (m.Success)
            {
                // Línea con modelo: "5700173375 2612481 P2GE 6C524 AB K006T91071XB 902 4G-VCT MPC"
                var partes = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                // Customer PO No: primer token de ≥7 dígitos puros
                var poNo = partes.FirstOrDefault(p => p.Length >= 7 && p.All(char.IsDigit)) ?? "";

                pendiente = new FdoLineaDto
                {
                    CustomerPoNo = poNo,
                    ModelNo      = m.Value,
                    ReqQty       = 0
                };
                continue;
            }

            // Línea de cantidades termina con "PC": "6,648 6,648 0 1,152 0 0 1,152 PC"
            // El token justo antes de "PC" es el Req. QTY
            if (pendiente != null
                && linea.TrimEnd().EndsWith("PC", StringComparison.OrdinalIgnoreCase))
            {
                var partes = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length >= 2)
                {
                    var qtyStr = partes[^2].Replace(",", "");
                    if (int.TryParse(qtyStr, out var qty) && qty > 0)
                    {
                        pendiente.ReqQty = qty;
                        resultado.Add(pendiente);
                        pendiente = null;
                    }
                }
            }
        }

        return resultado;
    }
}
