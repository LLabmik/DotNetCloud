using System.Text;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace DotNetCloud.Modules.Search.Extractors;

/// <summary>
/// Extracts text content from legacy .xls (Excel 97-2003) files using NPOI.
/// </summary>
public sealed class XlsContentExtractor : IContentExtractor
{
    /// <inheritdoc />
    public bool CanExtract(string mimeType)
    {
        return string.Equals(mimeType, "application/vnd.ms-excel", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var metadata = new Dictionary<string, string>();

        var workbook = new HSSFWorkbook(fileStream);

        var summary = workbook.SummaryInformation;
        if (summary?.Author is { } author && !string.IsNullOrWhiteSpace(author))
            metadata["author"] = author;
        if (summary?.Title is { } title && !string.IsNullOrWhiteSpace(title))
            metadata["title"] = title;

        metadata["sheetCount"] = workbook.NumberOfSheets.ToString();

        for (var sheetIndex = 0; sheetIndex < workbook.NumberOfSheets; sheetIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sheet = workbook.GetSheetAt(sheetIndex);
            if (sheet is null) continue;

            for (var rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                var row = sheet.GetRow(rowIndex);
                if (row is null) continue;

                var rowTexts = new List<string>();
                for (var cellIndex = row.FirstCellNum; cellIndex < row.LastCellNum; cellIndex++)
                {
                    var cell = row.GetCell(cellIndex);
                    if (cell is null) continue;

                    var value = GetCellText(cell);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        rowTexts.Add(value);
                    }
                }

                if (rowTexts.Count > 0)
                {
                    sb.AppendLine(string.Join(" ", rowTexts));
                }
            }
        }

        var result = new ExtractedContent
        {
            Text = sb.ToString(),
            Metadata = metadata
        };

        return Task.FromResult<ExtractedContent?>(result);
    }

    private static string GetCellText(ICell cell)
    {
        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue,
            CellType.Numeric => cell.NumericCellValue.ToString(),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.Formula => cell.CellFormula,
            _ => string.Empty
        };
    }
}
