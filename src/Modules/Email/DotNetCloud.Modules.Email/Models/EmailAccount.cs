namespace DotNetCloud.Modules.Email.Models;

/// <summary>
/// An email account configured by a user (IMAP/SMTP or Gmail).
/// </summary>
public sealed class EmailAccount
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who owns this account.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Provider type.</summary>
    public EmailProviderType ProviderType { get; set; }

    /// <summary>Display name (e.g., "Work Gmail").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Email address.</summary>
    public string EmailAddress { get; set; } = string.Empty;

    /// <summary>Encrypted credential blob (provider-specific JSON, Base64-encoded ciphertext).</summary>
    public string? EncryptedCredentialBlob { get; set; }

    /// <summary>Sync state as JSON (watermarks, cursors per mailbox).</summary>
    public string? SyncStateJson { get; set; }

    /// <summary>Whether the account is enabled for sync.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Soft-delete flag.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>When the account was soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>When the account was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the account was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Mailboxes belonging to this account.</summary>
    public ICollection<EmailMailbox> Mailboxes { get; set; } = new List<EmailMailbox>();
}

/// <summary>
/// Provider type for an email account.
/// </summary>
public enum EmailProviderType
{
    /// <summary>IMAP for receiving, SMTP for sending.</summary>
    ImapSmtp = 0,

    /// <summary>Gmail API.</summary>
    Gmail = 1
}
