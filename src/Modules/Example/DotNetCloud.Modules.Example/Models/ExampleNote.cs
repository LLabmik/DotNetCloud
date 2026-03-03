namespace DotNetCloud.Modules.Example.Models;

/// <summary>
/// A simple note entity used by the example module to demonstrate data modeling.
/// </summary>
public sealed record ExampleNote
{
    /// <summary>Unique identifier for the note.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Title of the note.</summary>
    public required string Title { get; init; }

    /// <summary>Body content of the note.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>User who created the note.</summary>
    public Guid CreatedByUserId { get; init; }

    /// <summary>When the note was created (UTC).</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>When the note was last updated (UTC).</summary>
    public DateTime? UpdatedAt { get; init; }
}
