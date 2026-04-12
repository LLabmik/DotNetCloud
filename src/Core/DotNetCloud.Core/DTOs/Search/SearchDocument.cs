namespace DotNetCloud.Core.DTOs.Search;

/// <summary>
/// Represents a single indexable item submitted to the search engine.
/// Modules convert their entities into this DTO for indexing.
/// </summary>
public sealed record SearchDocument
{
    /// <summary>The module that owns this document (e.g., "files", "notes", "chat").</summary>
    public required string ModuleId { get; init; }

    /// <summary>The entity identifier within the module (typically a Guid as string).</summary>
    public required string EntityId { get; init; }

    /// <summary>The entity type name (e.g., "Note", "Message", "FileNode").</summary>
    public required string EntityType { get; init; }

    /// <summary>Primary searchable title.</summary>
    public required string Title { get; init; }

    /// <summary>Body text or extracted content for full-text indexing.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>Short snippet for display in search results. May be null if not provided.</summary>
    public string? Summary { get; init; }

    /// <summary>The user who owns this entity, used for permission-scoped queries.</summary>
    public required Guid OwnerId { get; init; }

    /// <summary>Optional organization-level scoping identifier.</summary>
    public Guid? OrganizationId { get; init; }

    /// <summary>When the entity was originally created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When the entity was last updated.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Arbitrary metadata (tags, MIME type, path, etc.) stored alongside the index entry.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
