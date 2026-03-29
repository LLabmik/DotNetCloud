namespace DotNetCloud.Modules.Tracks.Models;

/// <summary>
/// Represents a file attachment on a card.
/// Can reference a file from the Files module (via FileNodeId) or an external URL.
/// </summary>
public sealed class CardAttachment
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The card this attachment belongs to.</summary>
    public Guid CardId { get; set; }

    /// <summary>
    /// Optional reference to a file in the Files module.
    /// Null if the attachment is an external URL.
    /// </summary>
    public Guid? FileNodeId { get; set; }

    /// <summary>Display file name.</summary>
    public required string FileName { get; set; }

    /// <summary>External URL (for non-Files attachments).</summary>
    public string? Url { get; set; }

    /// <summary>File size in bytes (null for external URLs).</summary>
    public long? FileSize { get; set; }

    /// <summary>MIME type of the file.</summary>
    public string? MimeType { get; set; }

    /// <summary>The user who uploaded this attachment.</summary>
    public Guid UploadedByUserId { get; set; }

    /// <summary>Timestamp when the attachment was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the card.</summary>
    public Card? Card { get; set; }
}
