using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Files.Events;

/// <summary>
/// Published when a file is uploaded or created.
/// </summary>
public sealed record FileUploadedEvent : IEvent
{
    /// <inheritdoc />
    public required Guid EventId { get; init; }

    /// <inheritdoc />
    public required DateTime CreatedAt { get; init; }

    /// <summary>The ID of the uploaded file node.</summary>
    public required Guid FileNodeId { get; init; }

    /// <summary>The file name.</summary>
    public required string FileName { get; init; }

    /// <summary>The file size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>The MIME type of the file.</summary>
    public string? MimeType { get; init; }

    /// <summary>The parent folder ID.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>The user who uploaded the file.</summary>
    public required Guid UploadedByUserId { get; init; }

    /// <summary>Relative content-addressable storage path (for media indexing). May be null for legacy events.</summary>
    public string? StoragePath { get; init; }
}
