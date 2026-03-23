namespace DotNetCloud.Modules.Contacts.Models;

/// <summary>
/// Represents a file attachment associated with a contact, including avatars.
/// Avatars are stored as attachments with <see cref="IsAvatar"/> set to true.
/// </summary>
public sealed class ContactAttachment
{
    /// <summary>Unique identifier for this attachment.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The contact this attachment belongs to.</summary>
    public Guid ContactId { get; set; }

    /// <summary>Original file name (e.g., "photo.jpg").</summary>
    public required string FileName { get; set; }

    /// <summary>MIME content type (e.g., "image/jpeg").</summary>
    public required string ContentType { get; set; }

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Relative storage path on disk.</summary>
    public required string StoragePath { get; set; }

    /// <summary>Whether this attachment is the contact's avatar image.</summary>
    public bool IsAvatar { get; set; }

    /// <summary>Optional description or label for the attachment.</summary>
    public string? Description { get; set; }

    /// <summary>When the attachment was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the attachment was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property to the owning contact.</summary>
    public Contact? Contact { get; set; }
}
