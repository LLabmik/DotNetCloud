namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a single vote in a planning poker session.
/// Each user may have one vote per round.
/// </summary>
public sealed class PokerVote
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The poker session this vote belongs to.</summary>
    public Guid SessionId { get; set; }

    /// <summary>The user who cast this vote.</summary>
    public Guid UserId { get; set; }

    /// <summary>The estimate value (e.g., "5", "M", "?").</summary>
    public required string Estimate { get; set; }

    /// <summary>Which round this vote was cast in.</summary>
    public int Round { get; set; }

    /// <summary>Timestamp when the vote was cast (UTC).</summary>
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the poker session.</summary>
    public PokerSession? Session { get; set; }
}
