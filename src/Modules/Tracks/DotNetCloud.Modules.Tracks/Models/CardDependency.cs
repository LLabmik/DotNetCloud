using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a dependency relationship between two cards.
/// </summary>
public sealed class CardDependency
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The card that has the dependency (the dependent card).</summary>
    public Guid CardId { get; set; }

    /// <summary>The card that is depended upon (the prerequisite).</summary>
    public Guid DependsOnCardId { get; set; }

    /// <summary>Type of dependency relationship.</summary>
    public CardDependencyType Type { get; set; } = CardDependencyType.BlockedBy;

    /// <summary>Timestamp when the dependency was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the dependent card.</summary>
    public Card? Card { get; set; }

    /// <summary>Navigation property to the prerequisite card.</summary>
    public Card? DependsOnCard { get; set; }
}
