using System.Data;
using System.Text;
using ExcelDataReader;
using PalletBalancer.Api.Models;

namespace PalletBalancer.Api.Services;

public static class MloXlsParser
{
    public static Mlo Parse(Stream stream, string fileName, int fdoId, DateOnly fechaEntrega)
    {
        // Required for .xls (BIFF format)
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });

        var sheet = dataSet.Tables[0];
        var mloNo = Path.GetFileNameWithoutExtension(fileName); // "MLO00302753"

        var mlo = new Mlo
        {
            MloNo        = mloNo,
            FdoId        = fdoId,
            FechaEntrega = fechaEntrega,
        };

        foreach (DataRow row in sheet.Rows)
        {
            var col0 = row[0]?.ToString()?.Trim() ?? "";

            // Skip header row (col0 = "FG Model") and footer (col0 starts with "-->")
            if (col0 == "FG Model" || col0.StartsWith("-->") || col0 == "")
                continue;

            if (row.ItemArray.Length <= 17) continue;

            var caseNo = row[5]?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(caseNo))
                continue;

            int fromQty = 0;
            try { fromQty = (int)Convert.ToDouble(row[7]); } catch { }

            mlo.Lineas.Add(new MloLinea
            {
                // col0 ("FG Model" en el header) contiene el número de parte (ModelNo).
                // col1 contiene la referencia FDO (SlipNo).
                ModelNo      = col0,
                SlipNo       = row[1]?.ToString()?.Trim() ?? "",
                Class        = row[3]?.ToString()?.Trim() ?? "",
                CaseNo       = caseNo,
                FromLocation = row[6]?.ToString()?.Trim() ?? "",
                FromQty      = fromQty,
                Check        = row[10]?.ToString()?.Trim() ?? "",
                Descripcion  = row[15]?.ToString()?.Trim() ?? "",
                ToLocation   = row[17]?.ToString()?.Trim() ?? "",
            });
        }

        return mlo;
    }

    /// <summary>
    /// Parses the numeric location segments for proximity sorting.
    /// "MEAX-FG1-56-17-02" → (56, 17, 2)
    /// </summary>
    public static (int Rack, int Pos, int Level) ParseLocation(string loc)
    {
        var parts = loc.Split('-');
        if (parts.Length < 3) return (0, 0, 0);
        // Last 3 numeric segments
        int.TryParse(parts[^3], out var rack);
        int.TryParse(parts[^2], out var pos);
        int.TryParse(parts[^1], out var lvl);
        return (rack, pos, lvl);
    }
}
