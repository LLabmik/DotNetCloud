using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a live review session where a host (PM/Scrum Master) navigates cards
/// and all participants follow in real-time. Supports integrated planning poker.
/// </summary>
public sealed class ReviewSession
{
    /// <summary>Unique identifier for this review session.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The board this review session is associated with.</summary>
    public Guid BoardId { get; set; }

    /// <summary>The user hosting this review session (typically PM or Scrum Master).</summary>
    public Guid HostUserId { get; set; }

    /// <summary>The card currently being reviewed. Null when no card is selected.</summary>
    public Guid? CurrentCardId { get; set; }

    /// <summary>Current session status.</summary>
    public ReviewSessionStatus Status { get; set; } = ReviewSessionStatus.Active;

    /// <summary>Timestamp when the session was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Timestamp when the session was ended (UTC). Null while active or paused.</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>Navigation property to the board.</summary>
    public Board? Board { get; set; }

    /// <summary>Navigation property to the current card.</summary>
    public Card? CurrentCard { get; set; }

    /// <summary>Participants in this review session.</summary>
    public ICollection<ReviewSessionParticipant> Participants { get; set; } = new List<ReviewSessionParticipant>();

    /// <summary>Poker sessions started during this review session.</summary>
    public ICollection<PokerSession> PokerSessions { get; set; } = new List<PokerSession>();
}
