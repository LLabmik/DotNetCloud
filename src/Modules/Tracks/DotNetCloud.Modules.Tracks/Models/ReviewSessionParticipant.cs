namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a participant in a live review session.
/// Tracks who has joined and their current connection status.
/// </summary>
public sealed class ReviewSessionParticipant
{
    /// <summary>Unique identifier for this participant record.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The review session this participant belongs to.</summary>
    public Guid ReviewSessionId { get; set; }

    /// <summary>The user who joined the review session.</summary>
    public Guid UserId { get; set; }

    /// <summary>Timestamp when the user joined (UTC).</summary>
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the user is currently connected via SignalR.</summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>Navigation property to the review session.</summary>
    public ReviewSession? ReviewSession { get; set; }
}
