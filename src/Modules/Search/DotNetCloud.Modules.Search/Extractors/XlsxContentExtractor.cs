using System.Text;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace DotNetCloud.Modules.Search.Extractors;

/// <summary>
/// Extracts text content from XLSX spreadsheets using Open XML SDK.
/// </summary>
public sealed class XlsxContentExtractor : IContentExtractor
{
    /// <inheritdoc />
    public bool CanExtract(string mimeType)
    {
        return string.Equals(mimeType,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public Task<ExtractedContent?> ExtractAsync(Stream fileStream, string mimeType, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        var metadata = new Dictionary<string, string>();

        using var document = SpreadsheetDocument.Open(fileStream, false);
        var workbookPart = document.WorkbookPart;

        if (workbookPart is null)
        {
            return Task.FromResult<ExtractedContent?>(null);
        }

        // Extract core properties
        var props = document.PackageProperties;
        if (props.Creator is { } author && !string.IsNullOrWhiteSpace(author))
            metadata["author"] = author;
        if (props.Title is { } title && !string.IsNullOrWhiteSpace(title))
            metadata["title"] = title;

        // Get shared strings table for resolving cell references
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

        var sheetCount = 0;
        foreach (var worksheetPart in workbookPart.WorksheetParts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sheetCount++;

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
            if (sheetData is null) continue;

            foreach (var row in sheetData.Elements<Row>())
            {
                var rowTexts = new List<string>();

                foreach (var cell in row.Elements<Cell>())
                {
                    var value = GetCellValue(cell, sharedStrings);
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

        metadata["sheetCount"] = sheetCount.ToString();

        var result = new ExtractedContent
        {
            Text = sb.ToString(),
            Metadata = metadata
        };

        return Task.FromResult<ExtractedContent?>(result);
    }

    private static string GetCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        if (cell.DataType is not null &&
            cell.DataType.Value == CellValues.SharedString &&
            sharedStrings is not null &&
            int.TryParse(cell.InnerText, out var index))
        {
            return sharedStrings.ElementAt(index).InnerText;
        }

        return cell.InnerText;
    }
}
