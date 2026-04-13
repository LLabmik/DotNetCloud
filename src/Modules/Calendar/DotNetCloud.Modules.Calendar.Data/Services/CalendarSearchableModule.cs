using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Data.Services;

/// <summary>
/// Exposes Calendar module data for full-text search indexing.
/// Provides calendar event title, description, and location as <see cref="SearchDocument"/> instances.
/// </summary>
public sealed class CalendarSearchableModule : ISearchableModule
{
    private readonly CalendarDbContext _db;
    private readonly ILogger<CalendarSearchableModule> _logger;

    /// <summary>Initializes a new instance of the <see cref="CalendarSearchableModule"/> class.</summary>
    public CalendarSearchableModule(CalendarDbContext db, ILogger<CalendarSearchableModule> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public string ModuleId => "calendar";

    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedEntityTypes { get; } = ["CalendarEvent"];

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchDocument>> GetAllSearchableDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var events = await _db.CalendarEvents
            .Include(e => e.Calendar)
            .Where(e => !e.IsDeleted)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} calendar events for search indexing", events.Count);

        return events.Select(ToSearchDocument).ToList();
    }

    /// <inheritdoc />
    public async Task<SearchDocument?> GetSearchableDocumentAsync(string entityId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(entityId, out var id))
            return null;

        var evt = await _db.CalendarEvents
            .Include(e => e.Calendar)
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);

        return evt is null ? null : ToSearchDocument(evt);
    }

    private static SearchDocument ToSearchDocument(Models.CalendarEvent evt)
    {
        var content = evt.Title;
        if (!string.IsNullOrEmpty(evt.Description))
            content += " " + evt.Description;
        if (!string.IsNullOrEmpty(evt.Location))
            content += " " + evt.Location;

        var metadata = new Dictionary<string, string>
        {
            ["StartUtc"] = evt.StartUtc.ToString("O"),
            ["EndUtc"] = evt.EndUtc.ToString("O"),
            ["Status"] = evt.Status.ToString()
        };
        if (evt.IsAllDay) metadata["AllDay"] = "true";
        if (!string.IsNullOrEmpty(evt.Location)) metadata["Location"] = evt.Location;
        if (evt.Calendar is not null) metadata["CalendarName"] = evt.Calendar.Name;

        return new SearchDocument
        {
            ModuleId = "calendar",
            EntityId = evt.Id.ToString(),
            EntityType = "CalendarEvent",
            Title = evt.Title,
            Content = content,
            Summary = BuildSummary(evt),
            OwnerId = evt.CreatedByUserId,
            CreatedAt = new DateTimeOffset(evt.CreatedAt, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(evt.UpdatedAt, TimeSpan.Zero),
            Metadata = metadata
        };
    }

    private static string BuildSummary(Models.CalendarEvent evt)
    {
        var date = evt.IsAllDay
            ? evt.StartUtc.ToString("MMM d, yyyy")
            : $"{evt.StartUtc:MMM d, yyyy h:mm tt} - {evt.EndUtc:h:mm tt}";
        var location = string.IsNullOrEmpty(evt.Location) ? "" : $" at {evt.Location}";
        return $"{date}{location}";
    }
}
