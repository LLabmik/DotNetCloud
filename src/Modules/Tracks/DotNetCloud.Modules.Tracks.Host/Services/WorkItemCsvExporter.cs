using System.Globalization;
using System.Text;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Host.Services;

/// <summary>
/// Exports work items to CSV format for spreadsheet import.
/// Non-technical users can download filtered views of their work items
/// to open in Excel, Google Sheets, or Numbers.
/// </summary>
public static class WorkItemCsvExporter
{
    private static readonly string[] Headers =
    [
        "Number", "Title", "Type", "Priority", "Status", "Assignee",
        "Story Points", "Due Date", "Labels", "Sprint"
    ];

    /// <summary>
    /// Generates a CSV file from a list of work items.
    /// The CSV can be opened directly in Excel, Google Sheets, or Numbers.
    /// </summary>
    public static byte[] ExportToCsv(IReadOnlyList<WorkItemDto> items)
    {
        var sb = new StringBuilder();

        // Header row
        sb.AppendLine(string.Join(",", Headers.Select(EscapeCsvField)));

        // Data rows
        foreach (var item in items)
        {
            var assignee = item.Assignments.Count > 0
                ? string.Join("; ", item.Assignments.Select(a => a.DisplayName ?? "Unknown"))
                : "Unassigned";

            var labels = item.Labels.Count > 0
                ? string.Join("; ", item.Labels.Select(l => l.Title))
                : "";

            sb.AppendLine(string.Join(",",
                EscapeCsvField(item.ItemNumber.ToString(CultureInfo.InvariantCulture)),
                EscapeCsvField(item.Title),
                EscapeCsvField(item.Type.ToString()),
                EscapeCsvField(item.Priority == Priority.None ? "" : item.Priority.ToString()),
                EscapeCsvField(item.SwimlaneTitle ?? ""),
                EscapeCsvField(assignee),
                EscapeCsvField(item.StoryPoints?.ToString(CultureInfo.InvariantCulture) ?? ""),
                EscapeCsvField(item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""),
                EscapeCsvField(labels),
                EscapeCsvField(item.SprintTitle ?? "")
            ));
        }

        // Use UTF-8 with BOM so Excel opens it correctly
        var preamble = Encoding.UTF8.GetPreamble();
        var data = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[preamble.Length + data.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(data, 0, result, preamble.Length, data.Length);
        return result;
    }

    /// <summary>
    /// Escapes a CSV field by wrapping in quotes if it contains commas, quotes, or newlines.
    /// </summary>
    private static string EscapeCsvField(string? field)
    {
        var value = field ?? "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            // Double up any embedded quotes and wrap in quotes
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
