using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A live review session scoped to an Epic where a host navigates items and participants follow in real-time.
/// </summary>
public sealed class ReviewSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EpicId { get; set; }
    public Guid HostUserId { get; set; }
    public Guid? CurrentItemId { get; set; }
    public ReviewSessionStatus Status { get; set; } = ReviewSessionStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    public WorkItem? Epic { get; set; }
    public WorkItem? CurrentItem { get; set; }
    public ICollection<ReviewSessionParticipant> Participants { get; set; } = new List<ReviewSessionParticipant>();
    public ICollection<PokerSession> PokerSessions { get; set; } = new List<PokerSession>();
}
