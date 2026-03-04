namespace DotNetCloud.Modules.Chat.Models;

/// <summary>
/// Represents a file attachment on a chat message.
/// References file nodes from the Files module for cross-module integration.
/// </summary>
public sealed class MessageAttachment
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Message this attachment belongs to.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Navigation property to the message.</summary>
    public Message? Message { get; set; }

    /// <summary>Reference to a FileNode in the Files module. Null if inline upload.</summary>
    public Guid? FileNodeId { get; set; }

    /// <summary>Display file name.</summary>
    public required string FileName { get; set; }

    /// <summary>MIME type of the attachment.</summary>
    public required string MimeType { get; set; }

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>Thumbnail URL for image/video previews.</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Sort order for multiple attachments on one message.</summary>
    public int SortOrder { get; set; }
}
