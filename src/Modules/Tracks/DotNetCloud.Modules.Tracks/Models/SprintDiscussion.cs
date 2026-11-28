namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A chat message posted during sprint planning or review sessions.
/// Scoped to a sprint or a review session — exactly one FK must be set.
/// </summary>
public sealed class SprintDiscussion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Sprint this message belongs to. Null if review-session-scoped.</summary>
    public Guid? SprintId { get; set; }

    /// <summary>Review session this message belongs to. Null if sprint-scoped.</summary>
    public Guid? ReviewSessionId { get; set; }

    /// <summary>User who sent the message. Cross-module ref, no DB FK.</summary>
    public Guid UserId { get; set; }

    /// <summary>Display name snapshot at post time (avoids cross-module lookup).</summary>
    public required string UserDisplayName { get; set; }

    /// <summary>Message content (plain text, max 2000 chars).</summary>
    public required string Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Sprint? Sprint { get; set; }
    public ReviewSession? ReviewSession { get; set; }
}
