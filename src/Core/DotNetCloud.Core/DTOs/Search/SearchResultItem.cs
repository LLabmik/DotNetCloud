namespace DotNetCloud.Core.DTOs.Search;

/// <summary>
/// An individual item in a full-text search result set.
/// </summary>
public sealed record SearchResultItem
{
    /// <summary>The module that owns this result (e.g., "files", "notes", "chat").</summary>
    public required string ModuleId { get; init; }

    /// <summary>The entity identifier within the source module.</summary>
    public required string EntityId { get; init; }

    /// <summary>The entity type name (e.g., "Note", "Message", "FileNode").</summary>
    public required string EntityType { get; init; }

    /// <summary>The result title, potentially with highlight markup.</summary>
    public required string Title { get; init; }

    /// <summary>A text excerpt around the matched terms, potentially with highlight markup.</summary>
    public string Snippet { get; init; } = string.Empty;

    /// <summary>Provider-specific relevance score (higher is more relevant).</summary>
    public double RelevanceScore { get; init; }

    /// <summary>When the source entity was last updated.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Arbitrary metadata from the source entity (tags, MIME type, etc.).</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
