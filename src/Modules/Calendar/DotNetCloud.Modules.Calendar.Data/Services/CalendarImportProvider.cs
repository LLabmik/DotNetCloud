using System.Globalization;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Import;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// Import provider that parses iCalendar (RFC 5545) data and creates calendar events.
/// Supports dry-run mode for previewing imports without persistence.
/// </summary>
public sealed class CalendarImportProvider : IImportProvider
{
    private readonly ICalendarEventService _eventService;
    private readonly ILogger<CalendarImportProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarImportProvider"/> class.
    /// </summary>
    public CalendarImportProvider(
        ICalendarEventService eventService,
        ILogger<CalendarImportProvider> logger)
    {
        _eventService = eventService;
        _logger = logger;
    }

    /// <inheritdoc />
    public ImportDataType DataType => ImportDataType.CalendarEvents;

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

        var parsedEvents = ParseICalEvents(request.Data);

        for (var i = 0; i < parsedEvents.Count; i++)
        {
            var parsed = parsedEvents[i];
            var displayName = parsed.Title ?? $"(event {i + 1})";

            try
            {
                if (string.IsNullOrWhiteSpace(parsed.Title))
                {
                    items.Add(new ImportItemResult
                    {
                        Index = i,
                        DisplayName = displayName,
                        Status = ImportItemStatus.Failed,
                        Message = "Missing required event title (SUMMARY property)."
                    });
                    continue;
                }

                if (!request.TargetContainerId.HasValue)
                {
                    items.Add(new ImportItemResult
                    {
                        Index = i,
                        DisplayName = displayName,
                        Status = ImportItemStatus.Failed,
                        Message = "Target calendar ID is required for calendar event imports."
                    });
                    continue;
                }

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
                    var dto = new CreateCalendarEventDto
                    {
                        CalendarId = request.TargetContainerId.Value,
                        Title = parsed.Title,
                        Description = parsed.Description,
                        Location = parsed.Location,
                        StartUtc = parsed.StartUtc,
                        EndUtc = parsed.EndUtc,
                        IsAllDay = parsed.IsAllDay,
                        RecurrenceRule = parsed.RecurrenceRule,
                        Url = parsed.Url
                    };

                    var created = await _eventService.CreateEventAsync(dto, caller, cancellationToken);
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
                _logger.LogWarning(ex, "Failed to import calendar event at index {Index}: {Title}", i, displayName);
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
            "Calendar import {Mode}: {Total} total, {Success} success, {Failed} failed for user {UserId}",
            dryRun ? "preview" : "execute",
            report.TotalItems, report.SuccessCount, report.FailedCount,
            caller.UserId);

        return report;
    }

    /// <summary>
    /// Parses iCalendar text into parsed event records.
    /// Extracted as internal for direct test access.
    /// </summary>
    internal static IReadOnlyList<ParsedCalendarEvent> ParseICalEvents(string icalText)
    {
        var events = new List<ParsedCalendarEvent>();
        var lines = icalText.Split(["\r\n", "\n"], StringSplitOptions.None);

        ParsedCalendarEvent? current = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = new ParsedCalendarEvent();
                continue;
            }

            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase) && current is not null)
            {
                events.Add(current);
                current = null;
                continue;
            }

            if (current is null) continue;

            if (line.StartsWith("SUMMARY:", StringComparison.OrdinalIgnoreCase))
                current.Title = UnescapeICalText(line[8..]);
            else if (line.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
                current.Description = UnescapeICalText(line[12..]);
            else if (line.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase))
                current.Location = UnescapeICalText(line[9..]);
            else if (line.StartsWith("URL:", StringComparison.OrdinalIgnoreCase))
                current.Url = line[4..];
            else if (line.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
                current.RecurrenceRule = line[6..];
            else if (line.StartsWith("DTSTART", StringComparison.OrdinalIgnoreCase))
            {
                var (dt, isAllDay) = ParseICalDateTime(line);
                current.StartUtc = dt;
                current.IsAllDay = isAllDay;
            }
            else if (line.StartsWith("DTEND", StringComparison.OrdinalIgnoreCase))
            {
                var (dt, _) = ParseICalDateTime(line);
                current.EndUtc = dt;
            }
        }

        return events;
    }

    private static (DateTime dt, bool isAllDay) ParseICalDateTime(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0) return (DateTime.UtcNow, false);

        var value = line[(colonIndex + 1)..].Trim();
        var isAllDay = line.Contains("VALUE=DATE", StringComparison.OrdinalIgnoreCase)
                       && !line.Contains("VALUE=DATE-TIME", StringComparison.OrdinalIgnoreCase);

        if (isAllDay && value.Length == 8)
        {
            if (DateTime.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dateOnly))
                return (dateOnly, true);
        }

        if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dtUtc))
            return (dtUtc, false);

        if (DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dtLocal))
            return (dtLocal, false);

        return (DateTime.UtcNow, false);
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
            DataType = ImportDataType.CalendarEvents,
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

    private static string UnescapeICalText(string text)
    {
        return text
            .Replace("\\n", "\n")
            .Replace("\\,", ",")
            .Replace("\\;", ";")
            .Replace("\\\\", "\\");
    }

    /// <summary>
    /// Represents a parsed calendar event from iCalendar data.
    /// </summary>
    internal sealed class ParsedCalendarEvent
    {
        /// <summary>Event title (SUMMARY).</summary>
        public string? Title { get; set; }

        /// <summary>Event description.</summary>
        public string? Description { get; set; }

        /// <summary>Event location.</summary>
        public string? Location { get; set; }

        /// <summary>Event URL.</summary>
        public string? Url { get; set; }

        /// <summary>Recurrence rule (RRULE).</summary>
        public string? RecurrenceRule { get; set; }

        /// <summary>Event start time in UTC.</summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Event end time in UTC.</summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow.AddHours(1);

        /// <summary>Whether this is an all-day event.</summary>
        public bool IsAllDay { get; set; }
    }
}
