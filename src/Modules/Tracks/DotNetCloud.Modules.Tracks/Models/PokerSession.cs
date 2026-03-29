using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a planning poker estimation session for a card.
/// Board members vote on story point estimates; votes are hidden until revealed.
/// </summary>
public sealed class PokerSession
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The card being estimated.</summary>
    public Guid CardId { get; set; }

    /// <summary>The board the card belongs to (denormalized for efficient queries).</summary>
    public Guid BoardId { get; set; }

    /// <summary>The user who started this session.</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>The estimation scale used.</summary>
    public PokerScale Scale { get; set; } = PokerScale.Fibonacci;

    /// <summary>Custom scale values when Scale is Custom (JSON array of strings).</summary>
    public string? CustomScaleValues { get; set; }

    /// <summary>Current session status.</summary>
    public PokerSessionStatus Status { get; set; } = PokerSessionStatus.Voting;

    /// <summary>The accepted estimate value (set when session completes).</summary>
    public string? AcceptedEstimate { get; set; }

    /// <summary>Current round number. Starts at 1, increments on re-vote.</summary>
    public int Round { get; set; } = 1;

    /// <summary>Timestamp when the session was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the session was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the card.</summary>
    public Card? Card { get; set; }

    /// <summary>Navigation property to the board.</summary>
    public Board? Board { get; set; }

    /// <summary>Votes cast in this session.</summary>
    public ICollection<PokerVote> Votes { get; set; } = new List<PokerVote>();
}
