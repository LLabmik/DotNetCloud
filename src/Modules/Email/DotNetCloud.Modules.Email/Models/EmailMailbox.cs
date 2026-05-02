namespace DotNetCloud.Modules.Email.Models;

/// <summary>
/// A mailbox or label within an email account.
/// </summary>
public sealed class EmailMailbox
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The parent account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Provider-specific identifier (IMAP folder name or Gmail label ID).</summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Bitmask of special folder flags (Inbox, Sent, Trash, etc.).</summary>
    public int SyncFlags { get; set; }

    /// <summary>Sort order for UI display.</summary>
    public int SortOrder { get; set; }

    /// <summary>Last sync time.</summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>When the mailbox record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Parent account navigation property.</summary>
    public EmailAccount? Account { get; set; }
}

/// <summary>
/// Bitmask flags for special mailbox types.
/// </summary>
[Flags]
public enum MailboxFlags
{
    /// <summary>Regular folder.</summary>
    None = 0,

    /// <summary>Inbox folder.</summary>
    Inbox = 1 << 0,

    /// <summary>Sent mail folder.</summary>
    Sent = 1 << 1,

    /// <summary>Trash folder.</summary>
    Trash = 1 << 2,

    /// <summary>Drafts folder.</summary>
    Drafts = 1 << 3,

    /// <summary>Archive folder.</summary>
    Archive = 1 << 4,

    /// <summary>Spam/junk folder.</summary>
    Spam = 1 << 5
}
