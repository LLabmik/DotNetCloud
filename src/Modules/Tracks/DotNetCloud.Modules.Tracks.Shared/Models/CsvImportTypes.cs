namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Result of parsing a CSV file header and preview.
/// </summary>
public sealed class CsvParseResult
{
    public List<string> Headers { get; set; } = [];
    public char Delimiter { get; set; }
    public List<List<string>> PreviewRows { get; set; } = [];
    public int TotalRows { get; set; }
}

/// <summary>
/// Maps CSV columns to work item fields.
/// </summary>
public sealed class CsvColumnMapping
{
    public int TitleColumn { get; set; } = -1;
    public int DescriptionColumn { get; set; } = -1;
    public int PriorityColumn { get; set; } = -1;
    public int TypeColumn { get; set; } = -1;
    public int StoryPointsColumn { get; set; } = -1;
    public int AssigneeEmailColumn { get; set; } = -1;
    public int DueDateColumn { get; set; } = -1;
    public int LabelsColumn { get; set; } = -1;
    public Dictionary<int, Guid> CustomFieldColumns { get; set; } = [];
}

/// <summary>
/// Validation errors for a single CSV row.
/// </summary>
public sealed class CsvRowError
{
    public int RowNumber { get; set; }
    public List<string> Values { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

/// <summary>
/// Result of CSV validation.
/// </summary>
public sealed class CsvValidationResult
{
    public List<CsvRowError> Errors { get; set; } = [];
    public int ValidRowCount { get; set; }
}

/// <summary>
/// Result of a CSV import operation.
/// </summary>
public sealed class CsvImportResult
{
    public int Created { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int BatchCount { get; set; }
    public List<string> Errors { get; set; } = [];
}
