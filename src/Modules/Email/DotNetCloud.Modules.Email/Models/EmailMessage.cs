namespace DotNetCloud.Modules.Email.Models;

/// <summary>
/// An email message within a thread.
/// </summary>
public sealed class EmailMessage
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The parent thread.</summary>
    public Guid ThreadId { get; set; }

    /// <summary>The parent account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>The mailbox this message belongs to.</summary>
    public Guid? MailboxId { get; set; }

    /// <summary>Provider-specific message identifier.</summary>
    public string ProviderMessageId { get; set; } = string.Empty;

    /// <summary>RFC 2822 Message-ID header.</summary>
    public string? MessageIdHeader { get; set; }

    /// <summary>In-Reply-To header value.</summary>
    public string? InReplyTo { get; set; }

    /// <summary>References header value.</summary>
    public string? References { get; set; }

    /// <summary>From address as JSON: [{"name":"...","email":"..."}].</summary>
    public string FromJson { get; set; } = "[]";

    /// <summary>To addresses as JSON.</summary>
    public string ToJson { get; set; } = "[]";

    /// <summary>Cc addresses as JSON.</summary>
    public string CcJson { get; set; } = "[]";

    /// <summary>Bcc addresses as JSON.</summary>
    public string BccJson { get; set; } = "[]";

    /// <summary>Message subject.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Text preview of the body (first 500 chars).</summary>
    public string? BodyPreview { get; set; }

    /// <summary>Full HTML body content of the message.</summary>
    public string? BodyHtml { get; set; }

    /// <summary>When the message was received (or the Date header value).</summary>
    public DateTime? DateReceived { get; set; }

    /// <summary>When the message was sent per the Date header.</summary>
    public DateTime? DateSent { get; set; }

    /// <summary>Whether the message has been read.</summary>
    public bool IsRead { get; set; }

    /// <summary>Whether the message is starred/flagged.</summary>
    public bool IsStarred { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Provider-specific flags/labels as JSON array.</summary>
    public string? FlagsJson { get; set; }

    /// <summary>When the message record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the message record was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Parent thread navigation property.</summary>
    public EmailThread? Thread { get; set; }

    /// <summary>Attachments for this message.</summary>
    public ICollection<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
}
