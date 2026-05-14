namespace DotNetCloud.Core.DTOs;

/// <summary>
/// DTO for user presence information.
/// </summary>
public sealed record PresenceDto
{
    /// <summary>User ID.</summary>
    public Guid UserId { get; init; }

    /// <summary>Presence status: "Online", "Away", "DoNotDisturb", "Offline".</summary>
    public required string Status { get; init; }

    /// <summary>Custom status message.</summary>
    public string? StatusMessage { get; init; }

    /// <summary>Last seen timestamp (UTC).</summary>
    public DateTime? LastSeenAt { get; init; }
}
