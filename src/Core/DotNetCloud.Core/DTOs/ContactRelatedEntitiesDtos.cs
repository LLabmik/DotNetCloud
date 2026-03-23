namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Related entities associated with a contact.
/// </summary>
public sealed record ContactRelatedEntitiesDto
{
    /// <summary>
    /// Related calendar events associated with the contact.
    /// </summary>
    public IReadOnlyList<CalendarEventSummaryDto> Events { get; init; } = [];

    /// <summary>
    /// Related notes linked to the contact.
    /// </summary>
    public IReadOnlyList<NoteSummaryDto> Notes { get; init; } = [];
}

/// <summary>
/// Summary of a calendar event for related-entities displays.
/// </summary>
public sealed record CalendarEventSummaryDto
{
    /// <summary>Event identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Event title.</summary>
    public required string Title { get; init; }

    /// <summary>Start time in UTC.</summary>
    public required DateTime StartUtc { get; init; }

    /// <summary>End time in UTC.</summary>
    public required DateTime EndUtc { get; init; }
}

/// <summary>
/// Summary of a note for related-entities displays.
/// </summary>
public sealed record NoteSummaryDto
{
    /// <summary>Note identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Note title.</summary>
    public required string Title { get; init; }

    /// <summary>Last updated timestamp in UTC.</summary>
    public required DateTime UpdatedAt { get; init; }
}
