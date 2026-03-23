using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Import;
using DotNetCloud.Modules.Notes.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Notes.Data.Services;

/// <summary>
/// Import provider that parses note data (JSON manifest or raw Markdown/plain text)
/// and creates notes. Supports dry-run mode for previewing imports without persistence.
/// </summary>
/// <remarks>
/// Supported input formats:
/// <list type="bullet">
///   <item><description>
///     <b>JSON manifest</b> — a JSON array of objects with "title", "content", "format" (optional, defaults to Markdown),
///     and "tags" (optional) properties. Enables bulk import of multiple notes.
///   </description></item>
///   <item><description>
///     <b>Raw Markdown/plain text</b> — treated as a single note. Title is extracted from the first
///     <c># heading</c> line or defaults to "Imported Note".
///   </description></item>
/// </list>
/// </remarks>
public sealed class NotesImportProvider : IImportProvider
{
    private readonly INoteService _noteService;
    private readonly ILogger<NotesImportProvider> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="NotesImportProvider"/> class.
    /// </summary>
    public NotesImportProvider(
        INoteService noteService,
        ILogger<NotesImportProvider> logger)
    {
        _noteService = noteService;
        _logger = logger;
    }

    /// <inheritdoc />
    public ImportDataType DataType => ImportDataType.Notes;

    /// <inheritdoc />
    public Task<ImportReport> PreviewAsync(
        ImportRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        return BuildReportAsync(request, caller, dryRun: true, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ImportReport> ExecuteAsync(
        ImportRequest request,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (request.DryRun)
        {
            return PreviewAsync(request, caller, cancellationToken);
        }

        return BuildReportAsync(request, caller, dryRun: false, cancellationToken);
    }

    private async Task<ImportReport> BuildReportAsync(
        ImportRequest request,
        CallerContext caller,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTime.UtcNow;
        var items = new List<ImportItemResult>();

        if (string.IsNullOrWhiteSpace(request.Data))
        {
            return CreateReport(request, items, dryRun, startedAt);
        }

        var parsedNotes = ParseNotes(request.Data);

        for (var i = 0; i < parsedNotes.Count; i++)
        {
            var parsed = parsedNotes[i];
            var displayName = parsed.Title;

            try
            {
                if (string.IsNullOrWhiteSpace(parsed.Title))
                {
                    items.Add(new ImportItemResult
                    {
                        Index = i,
                        DisplayName = $"(note {i + 1})",
                        Status = ImportItemStatus.Failed,
                        Message = "Missing required note title."
                    });
                    continue;
                }

                var dto = new CreateNoteDto
                {
                    Title = parsed.Title,
                    Content = parsed.Content,
                    Format = parsed.Format,
                    FolderId = request.TargetContainerId,
                    Tags = parsed.Tags
                };

                if (dryRun)
                {
                    items.Add(new ImportItemResult
                    {
                        Index = i,
                        DisplayName = displayName,
                        Status = ImportItemStatus.Success,
                        Message = "Would be imported."
                    });
                }
                else
                {
                    var created = await _noteService.CreateNoteAsync(dto, caller, cancellationToken);
                    items.Add(new ImportItemResult
                    {
                        Index = i,
                        DisplayName = displayName,
                        Status = ImportItemStatus.Success,
                        RecordId = created.Id
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to import note at index {Index}: {Title}", i, displayName);
                items.Add(new ImportItemResult
                {
                    Index = i,
                    DisplayName = displayName,
                    Status = ImportItemStatus.Failed,
                    Message = ex.Message
                });
            }
        }

        var report = CreateReport(request, items, dryRun, startedAt);
        _logger.LogInformation(
            "Notes import {Mode}: {Total} total, {Success} success, {Failed} failed for user {UserId}",
            dryRun ? "preview" : "execute",
            report.TotalItems, report.SuccessCount, report.FailedCount,
            caller.UserId);

        return report;
    }

    /// <summary>
    /// Parses note import data. Supports JSON manifest (array) or raw Markdown/plain text.
    /// Extracted as internal for direct test access.
    /// </summary>
    internal static IReadOnlyList<ParsedNote> ParseNotes(string data)
    {
        var trimmed = data.TrimStart();

        // Try JSON manifest first (starts with '[')
        if (trimmed.StartsWith('['))
        {
            try
            {
                var entries = JsonSerializer.Deserialize<List<NoteImportEntry>>(trimmed, JsonOptions);
                if (entries is not null)
                {
                    return entries.Select(e => new ParsedNote
                    {
                        Title = e.Title ?? string.Empty,
                        Content = e.Content ?? string.Empty,
                        Format = string.Equals(e.Format, "plaintext", StringComparison.OrdinalIgnoreCase)
                            ? NoteContentFormat.PlainText
                            : NoteContentFormat.Markdown,
                        Tags = e.Tags ?? []
                    }).ToList();
                }
            }
            catch (JsonException)
            {
                // Fall through to raw text handling
            }
        }

        // Raw Markdown/plain text — single note
        return [ParseSingleNote(data)];
    }

    /// <summary>
    /// Parses a single raw Markdown or plain text document into a note.
    /// Title is extracted from the first # heading if present.
    /// </summary>
    internal static ParsedNote ParseSingleNote(string content)
    {
        var lines = content.Split('\n');
        string? title = null;
        var contentStartIndex = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            if (trimmed.StartsWith("# "))
            {
                title = trimmed[2..].Trim();
                contentStartIndex = i + 1;
            }
            break;
        }

        title ??= "Imported Note";

        // Reconstruct content without the title line
        var bodyContent = contentStartIndex > 0
            ? string.Join('\n', lines[contentStartIndex..]).TrimStart('\n', '\r')
            : content;

        return new ParsedNote
        {
            Title = title,
            Content = bodyContent,
            Format = NoteContentFormat.Markdown,
            Tags = []
        };
    }

    private static ImportReport CreateReport(
        ImportRequest request,
        IReadOnlyList<ImportItemResult> items,
        bool dryRun,
        DateTime startedAt)
    {
        return new ImportReport
        {
            IsDryRun = dryRun,
            DataType = ImportDataType.Notes,
            Source = request.Source,
            TotalItems = items.Count,
            SuccessCount = items.Count(i => i.Status == ImportItemStatus.Success),
            SkippedCount = items.Count(i => i.Status == ImportItemStatus.Skipped),
            FailedCount = items.Count(i => i.Status == ImportItemStatus.Failed),
            ConflictCount = items.Count(i => i.Status == ImportItemStatus.Conflict),
            Items = items,
            ConflictStrategy = request.ConflictStrategy,
            StartedAtUtc = startedAt,
            CompletedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Represents a parsed note from import data.
    /// </summary>
    internal sealed class ParsedNote
    {
        /// <summary>Note title.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Note content body.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Content format.</summary>
        public NoteContentFormat Format { get; set; } = NoteContentFormat.Markdown;

        /// <summary>Tags to apply.</summary>
        public IReadOnlyList<string> Tags { get; set; } = [];
    }

    private sealed class NoteImportEntry
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Format { get; set; }
        public List<string>? Tags { get; set; }
    }
}
