using System.Globalization;
using System.Text;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Parses, validates, and imports work items from CSV files.
/// Supports auto-detection of delimiters, BOM handling, and field mapping.
/// </summary>
public sealed class CsvImportService
{
    private readonly TracksDbContext _db;
    private readonly WorkItemService _workItemService;
    private readonly IUserDirectory? _userDirectory;
    private readonly ILogger<CsvImportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvImportService"/> class.
    /// </summary>
    public CsvImportService(
        TracksDbContext db,
        WorkItemService workItemService,
        ILogger<CsvImportService> logger,
        IUserDirectory? userDirectory = null)
    {
        _db = db;
        _workItemService = workItemService;
        _logger = logger;
        _userDirectory = userDirectory;
    }

    /// <summary>
    /// Parses a CSV stream and returns detected columns + raw preview rows.
    /// </summary>
    public async Task<CsvParseResult> ParseCsvAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var allLines = new List<string>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            allLines.Add(line);
        }

        if (allLines.Count == 0)
        {
            throw new InvalidOperationException("CSV file is empty.");
        }

        // Auto-detect delimiter
        char delimiter = DetectDelimiter(allLines[0]);

        // Parse header
        var headers = ParseCsvLine(allLines[0], delimiter);

        // Parse preview rows (up to 5)
        var previewRows = new List<List<string>>();
        for (int i = 1; i < Math.Min(allLines.Count, 6); i++)
        {
            var row = ParseCsvLine(allLines[i], delimiter);
            previewRows.Add(row);
        }

        return new CsvParseResult
        {
            Headers = headers,
            Delimiter = delimiter,
            PreviewRows = previewRows,
            TotalRows = allLines.Count - 1 // Excluding header
        };
    }

    /// <summary>
    /// Validates mapped rows and returns row-level errors without importing.
    /// </summary>
    public async Task<CsvValidationResult> ValidateCsvAsync(
        Guid productId,
        Stream stream,
        CsvColumnMapping mapping,
        CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var allLines = new List<string>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            allLines.Add(line);
        }

        if (allLines.Count < 2)
        {
            return new CsvValidationResult { Errors = [], ValidRowCount = 0 };
        }

        char delimiter = DetectDelimiter(allLines[0]);
        var errors = new List<CsvRowError>();
        var headers = ParseCsvLine(allLines[0], delimiter);

        // Resolve known users by email for assignee mapping
        var userEmails = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (mapping.AssigneeEmailColumn >= 0 && _userDirectory is not null)
        {
            // Collect emails and search for users
            for (int i = 1; i < allLines.Count; i++)
            {
                var row = ParseCsvLine(allLines[i], delimiter);
                var email = mapping.AssigneeEmailColumn < row.Count ? row[mapping.AssigneeEmailColumn]?.Trim() : null;
                if (!string.IsNullOrWhiteSpace(email) && !userEmails.ContainsKey(email))
                {
                    var results = await _userDirectory.SearchUsersAsync(email, maxResults: 1, ct);
                    if (results.Count > 0)
                        userEmails[email] = results[0].Id;
                }
            }
        }

        // Validate each row
        for (int i = 1; i < allLines.Count; i++)
        {
            var row = ParseCsvLine(allLines[i], delimiter);
            if (row.All(string.IsNullOrWhiteSpace)) continue;

            var rowErrors = new List<string>();

            // Validate title
            if (mapping.TitleColumn >= 0 && mapping.TitleColumn < row.Count)
            {
                var title = row[mapping.TitleColumn]?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                    rowErrors.Add("Title is required.");
            }
            else
            {
                rowErrors.Add("Title column not mapped.");
            }

            // Validate priority
            if (mapping.PriorityColumn >= 0 && mapping.PriorityColumn < row.Count)
            {
                var priorityStr = row[mapping.PriorityColumn]?.Trim();
                if (!string.IsNullOrWhiteSpace(priorityStr) &&
                    !Enum.TryParse<Priority>(priorityStr, ignoreCase: true, out _))
                {
                    rowErrors.Add($"Invalid priority: '{priorityStr}'. Expected: None, Low, Medium, High, Critical.");
                }
            }

            // Validate type
            if (mapping.TypeColumn >= 0 && mapping.TypeColumn < row.Count)
            {
                var typeStr = row[mapping.TypeColumn]?.Trim();
                if (!string.IsNullOrWhiteSpace(typeStr) &&
                    !Enum.TryParse<WorkItemType>(typeStr, ignoreCase: true, out _))
                {
                    rowErrors.Add($"Invalid type: '{typeStr}'. Expected: Epic, Feature, Item, SubItem.");
                }
            }

            // Validate story points
            if (mapping.StoryPointsColumn >= 0 && mapping.StoryPointsColumn < row.Count)
            {
                var spStr = row[mapping.StoryPointsColumn]?.Trim();
                if (!string.IsNullOrWhiteSpace(spStr) && !int.TryParse(spStr, out _))
                {
                    rowErrors.Add($"Invalid story points: '{spStr}'. Must be a number.");
                }
            }

            // Validate due date
            if (mapping.DueDateColumn >= 0 && mapping.DueDateColumn < row.Count)
            {
                var dateStr = row[mapping.DueDateColumn]?.Trim();
                if (!string.IsNullOrWhiteSpace(dateStr) &&
                    !DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
                {
                    rowErrors.Add($"Invalid date: '{dateStr}'.");
                }
            }

            // Validate assignee
            if (mapping.AssigneeEmailColumn >= 0 && mapping.AssigneeEmailColumn < row.Count)
            {
                var email = row[mapping.AssigneeEmailColumn]?.Trim();
                if (!string.IsNullOrWhiteSpace(email) && !userEmails.ContainsKey(email))
                {
                    rowErrors.Add($"Unknown user email: '{email}'.");
                }
            }

            if (rowErrors.Count > 0)
            {
                errors.Add(new CsvRowError
                {
                    RowNumber = i,
                    Values = row,
                    Errors = rowErrors
                });
            }
        }

        return new CsvValidationResult
        {
            Errors = errors,
            ValidRowCount = allLines.Count - 1 - errors.Count
        };
    }

    /// <summary>
    /// Imports work items from a CSV stream with the specified column mapping.
    /// </summary>
    public async Task<CsvImportResult> ImportCsvAsync(
        Guid productId,
        Guid swimlaneId,
        Guid createdByUserId,
        Stream stream,
        CsvColumnMapping mapping,
        bool skipDuplicates,
        CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var allLines = new List<string>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            allLines.Add(line);
        }

        if (allLines.Count < 2)
        {
            return new CsvImportResult { Created = 0, Failed = 0, Errors = [], BatchCount = 0 };
        }

        char delimiter = DetectDelimiter(allLines[0]);
        var headers = ParseCsvLine(allLines[0], delimiter);

        // Resolve known users by email
        var userEmails = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (mapping.AssigneeEmailColumn >= 0 && _userDirectory is not null)
        {
            for (int i = 1; i < allLines.Count; i++)
            {
                var row = ParseCsvLine(allLines[i], delimiter);
                var email = mapping.AssigneeEmailColumn < row.Count ? row[mapping.AssigneeEmailColumn]?.Trim() : null;
                if (!string.IsNullOrWhiteSpace(email) && !userEmails.ContainsKey(email))
                {
                    var results = await _userDirectory.SearchUsersAsync(email, maxResults: 1, ct);
                    if (results.Count > 0)
                        userEmails[email] = results[0].Id;
                }
            }
        }

        // Get existing titles for duplicate detection
        var existingTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (skipDuplicates)
        {
            var titles = await _db.WorkItems
                .Where(wi => wi.ProductId == productId && !wi.IsDeleted)
                .Select(wi => wi.Title)
                .ToListAsync(ct);
            foreach (var title in titles)
                existingTitles.Add(title);
        }

        var result = new CsvImportResult();
        var batch = new List<WorkItem>(50);

        for (int i = 1; i < allLines.Count; i++)
        {
            var row = ParseCsvLine(allLines[i], delimiter);
            if (row.All(string.IsNullOrWhiteSpace)) continue;

            try
            {
                var title = mapping.TitleColumn >= 0 && mapping.TitleColumn < row.Count
                    ? row[mapping.TitleColumn]?.Trim()
                    : null;

                if (string.IsNullOrWhiteSpace(title))
                {
                    result.Failed++;
                    result.Errors.Add($"Row {i}: Missing title.");
                    continue;
                }

                if (skipDuplicates && existingTitles.Contains(title))
                {
                    result.Skipped++;
                    continue;
                }

                var dto = new CreateWorkItemDto
                {
                    Title = title,
                    Description = mapping.DescriptionColumn >= 0 && mapping.DescriptionColumn < row.Count
                        ? row[mapping.DescriptionColumn]?.Trim()
                        : null,
                    Priority = mapping.PriorityColumn >= 0 && mapping.PriorityColumn < row.Count
                        && Enum.TryParse<Priority>(row[mapping.PriorityColumn]?.Trim(), ignoreCase: true, out var p)
                        ? p : Core.DTOs.Priority.None,
                    StoryPoints = mapping.StoryPointsColumn >= 0 && mapping.StoryPointsColumn < row.Count
                        && int.TryParse(row[mapping.StoryPointsColumn]?.Trim(), out var sp)
                        ? sp : null,
                    DueDate = mapping.DueDateColumn >= 0 && mapping.DueDateColumn < row.Count
                        && DateTime.TryParse(row[mapping.DueDateColumn]?.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dd)
                        ? dd : null
                };

                // Determine type
                WorkItemType type = WorkItemType.Item;
                if (mapping.TypeColumn >= 0 && mapping.TypeColumn < row.Count)
                {
                    var typeStr = row[mapping.TypeColumn]?.Trim();
                    if (!string.IsNullOrWhiteSpace(typeStr) &&
                        Enum.TryParse<WorkItemType>(typeStr, ignoreCase: true, out var parsedType))
                    {
                        type = parsedType;
                    }
                }

                // Create the work item
                var workItem = await _workItemService.CreateWorkItemAsync(
                    productId, swimlaneId, type, createdByUserId, dto, ct);

                // Assign user
                if (mapping.AssigneeEmailColumn >= 0 && mapping.AssigneeEmailColumn < row.Count)
                {
                    var email = row[mapping.AssigneeEmailColumn]?.Trim();
                    if (!string.IsNullOrWhiteSpace(email) && userEmails.TryGetValue(email, out var userId))
                    {
                        var assignment = new WorkItemAssignment
                        {
                            WorkItemId = workItem.Id,
                            UserId = userId
                        };
                        _db.WorkItemAssignments.Add(assignment);
                    }
                }

                result.Created++;

                if (result.Created % 50 == 0)
                {
                    result.BatchCount++;
                    await _db.SaveChangesAsync(ct);
                    _logger.LogInformation("CSV import batch {Batch}: {Count} items created", result.BatchCount, result.Created);
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"Row {i}: {ex.Message}");
                _logger.LogWarning(ex, "CSV import error at row {Row}", i);
            }
        }

        // Save remaining
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("CSV import complete: {Created} created, {Skipped} skipped, {Failed} failed",
            result.Created, result.Skipped, result.Failed);

        return result;
    }

    /// <summary>
    /// Detects the most likely delimiter character from a header row.
    /// </summary>
    private static char DetectDelimiter(string headerLine)
    {
        var candidates = new Dictionary<char, int>
        {
            [','] = 0,
            ['\t'] = 0,
            [';'] = 0,
            ['|'] = 0
        };

        foreach (char c in headerLine)
        {
            if (candidates.ContainsKey(c))
                candidates[c]++;
        }

        return candidates.MaxBy(kv => kv.Value).Key;
    }

    /// <summary>
    /// Parses a single CSV line into a list of field values, handling quoted fields.
    /// </summary>
    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString().Trim());

        if (fields.Count > 0 && fields[0].StartsWith('\uFEFF'))
        {
            fields[0] = fields[0][1..];
        }

        return fields;
    }
}
