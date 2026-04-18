using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Calendar.Services;

/// <summary>
/// HTTP API client for Calendar REST endpoints.
/// </summary>
public interface ICalendarApiClient
{
    Task<IReadOnlyList<CalendarDto>> ListCalendarsAsync(CancellationToken cancellationToken = default);
    Task<CalendarDto?> CreateCalendarAsync(CreateCalendarDto dto, CancellationToken cancellationToken = default);
    Task<CalendarDto?> UpdateCalendarAsync(Guid calendarId, UpdateCalendarDto dto, CancellationToken cancellationToken = default);
    Task DeleteCalendarAsync(Guid calendarId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CalendarEventDto>> ListEventsAsync(Guid calendarId, DateTime? startUtc, DateTime? endUtc, CancellationToken cancellationToken = default);
    Task<CalendarEventDto?> CreateEventAsync(CreateCalendarEventDto dto, CancellationToken cancellationToken = default);
    Task<CalendarEventDto?> UpdateEventAsync(Guid eventId, UpdateCalendarEventDto dto, CancellationToken cancellationToken = default);
    Task<CalendarEventDto?> GetEventAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default);

    // RSVP
    Task<CalendarEventDto?> RsvpAsync(Guid eventId, EventRsvpDto rsvp, CancellationToken cancellationToken = default);

    // Search
    Task<IReadOnlyList<CalendarEventDto>> SearchEventsAsync(string? query, DateTime? from, DateTime? to, int skip = 0, int take = 50, CancellationToken cancellationToken = default);

    // Sharing
    Task<IReadOnlyList<CalendarShareResponse>> ListSharesAsync(Guid calendarId, CancellationToken cancellationToken = default);
    Task<CalendarShareResponse?> ShareCalendarAsync(Guid calendarId, Guid? userId, Guid? teamId, string permission = "ReadOnly", CancellationToken cancellationToken = default);
    Task RevokeShareAsync(Guid shareId, CancellationToken cancellationToken = default);

    // Import/Export
    Task<string> ExportCalendarICalAsync(Guid calendarId, CancellationToken cancellationToken = default);
    Task ImportICalAsync(Guid calendarId, string iCalText, CancellationToken cancellationToken = default);
}

/// <summary>
/// Share response deserialized from the CalendarShare entity returned by the server.
/// </summary>
public sealed record CalendarShareResponse
{
    /// <summary>Share ID.</summary>
    public Guid Id { get; init; }

    /// <summary>Calendar ID.</summary>
    public Guid CalendarId { get; init; }

    /// <summary>User the calendar is shared with.</summary>
    public Guid? SharedWithUserId { get; init; }

    /// <summary>Team the calendar is shared with.</summary>
    public Guid? SharedWithTeamId { get; init; }

    /// <summary>Permission level.</summary>
    public string Permission { get; init; } = "ReadOnly";

    /// <summary>When the share was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>User who created the share.</summary>
    public Guid? CreatedByUserId { get; init; }
}
