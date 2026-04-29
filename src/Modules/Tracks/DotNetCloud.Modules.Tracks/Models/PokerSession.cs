using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// A planning poker estimation session scoped to an Epic, estimating a specific Item.
/// </summary>
public sealed class PokerSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EpicId { get; set; }
    public Guid ItemId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public PokerScale Scale { get; set; } = PokerScale.Fibonacci;
    public string? CustomScaleValues { get; set; }
    public PokerSessionStatus Status { get; set; } = PokerSessionStatus.Voting;
    public string? AcceptedEstimate { get; set; }
    public int Round { get; set; } = 1;
    public Guid? ReviewSessionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public WorkItem? Epic { get; set; }
    public WorkItem? Item { get; set; }
    public ReviewSession? ReviewSession { get; set; }
    public ICollection<PokerVote> Votes { get; set; } = new List<PokerVote>();
}
