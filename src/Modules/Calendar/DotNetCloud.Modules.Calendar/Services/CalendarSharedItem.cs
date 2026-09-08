using DotNetCloud.Modules.Calendar.Models;

namespace DotNetCloud.Modules.Calendar.Services;

/// <summary>
/// A calendar shared with the caller (direct user share or a share targeting a team the caller
/// belongs to). Returned by the Calendar host "shared with me" endpoint and consumed by the
/// Files shared-with-me aggregator and by module UIs that need to surface shared calendars.
/// </summary>
public sealed record CalendarSharedItem
{
    /// <summary>The unique identifier of the shared calendar.</summary>
    public Guid CalendarId { get; init; }

    /// <summary>The calendar's display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The user who owns the calendar.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>The user who created the share (normally the calendar owner).</summary>
    public Guid? CreatedByUserId { get; init; }

    /// <summary>User this is shared with (null for team shares).</summary>
    public Guid? SharedWithUserId { get; init; }

    /// <summary>Team this is shared with (null for user shares).</summary>
    public Guid? SharedWithTeamId { get; init; }

    /// <summary>Permission level granted by the share.</summary>
    public CalendarSharePermission Permission { get; init; } = CalendarSharePermission.ReadOnly;

    /// <summary>When the share was created (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the calendar was last modified (UTC) — display/ordering fallback.</summary>
    public DateTime UpdatedAt { get; init; }
}
