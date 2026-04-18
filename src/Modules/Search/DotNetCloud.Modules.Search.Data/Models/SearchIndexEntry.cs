namespace DotNetCloud.Modules.Search.Data.Models;

/// <summary>
/// Represents a single entry in the search index.
/// Each entry corresponds to a searchable entity from a module (file, note, message, etc.).
/// </summary>
public sealed class SearchIndexEntry
{
    /// <summary>Auto-incrementing primary key.</summary>
    public long Id { get; set; }

    /// <summary>The module that owns this entity (e.g., "files", "notes", "chat").</summary>
    public required string ModuleId { get; set; }

    /// <summary>The entity identifier within the source module.</summary>
    public required string EntityId { get; set; }

    /// <summary>The entity type name (e.g., "Note", "Message", "FileNode").</summary>
    public required string EntityType { get; set; }

    /// <summary>Primary searchable title.</summary>
    public required string Title { get; set; }

    /// <summary>Full-text indexed body content (extracted text, message body, etc.).</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Short snippet for display in search results.</summary>
    public string? Summary { get; set; }

    /// <summary>The user who owns this entity, used for permission-scoped queries.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Optional organization-level scoping identifier.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>When the source entity was originally created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the source entity was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When this entry was last indexed.</summary>
    public DateTimeOffset IndexedAt { get; set; }

    /// <summary>Serialized metadata dictionary (JSON). Contains tags, MIME type, path, etc.</summary>
    public string? MetadataJson { get; set; }
}
