namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a user assignment to a card.
/// </summary>
public sealed class CardAssignment
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The card this assignment belongs to.</summary>
    public Guid CardId { get; set; }

    /// <summary>The assigned user's ID.</summary>
    public Guid UserId { get; set; }

    /// <summary>Timestamp when the assignment was created (UTC).</summary>
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the card.</summary>
    public Card? Card { get; set; }
}
