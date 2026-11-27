namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// An emoji reaction on a work item comment. Composite key on CommentId + UserId + Emoji
/// ensures each user can only add one of each emoji per comment.
/// </summary>
public sealed class CommentReaction
{
    /// <summary>The comment this reaction belongs to.</summary>
    public Guid CommentId { get; set; }

    /// <summary>The user who reacted.</summary>
    public Guid UserId { get; set; }

    /// <summary>The emoji character (e.g., 👍, ❤️, 😄).</summary>
    public required string Emoji { get; set; }

    /// <summary>When the reaction was added.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the parent comment.</summary>
    public WorkItemComment? Comment { get; set; }
}
