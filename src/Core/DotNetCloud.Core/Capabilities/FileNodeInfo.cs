namespace DotNetCloud.Core.Capabilities;

/// <summary>
/// Minimal read-only representation of a file or folder from the Files module,
/// for use in cross-module file browsing (e.g., email compose file picker).
/// </summary>
public sealed record FileNodeInfo
{
    /// <summary>Unique identifier of the file or folder node.</summary>
    public required Guid Id { get; init; }

    /// <summary>Display name (e.g., "report.pdf", "Photos").</summary>
    public required string Name { get; init; }

    /// <summary>Node type: "File", "Folder", or "SymbolicLink".</summary>
    public required string NodeType { get; init; }

    /// <summary>MIME content type (null for folders).</summary>
    public string? MimeType { get; init; }

    /// <summary>File size in bytes (0 for folders).</summary>
    public long Size { get; init; }

    /// <summary>Parent folder ID. Null for root-level nodes.</summary>
    public Guid? ParentId { get; init; }
}
