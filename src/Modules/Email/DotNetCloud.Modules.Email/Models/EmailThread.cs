namespace DotNetCloud.Modules.Email.Models;

/// <summary>
/// An email thread grouping related messages.
/// </summary>
public sealed class EmailThread
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The parent account.</summary>
    public Guid AccountId { get; set; }

    /// <summary>Provider-specific thread ID (Gmail threadId or computed from headers).</summary>
    public string ProviderThreadId { get; set; } = string.Empty;

    /// <summary>Canonical subject for grouping.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Preview snippet from the latest message.</summary>
    public string? Snippet { get; set; }

    /// <summary>Aggregated unique participants as JSON array.</summary>
    public string ParticipantsJson { get; set; } = "[]";

    /// <summary>Number of messages in the thread.</summary>
    public int MessageCount { get; set; }

    /// <summary>Timestamp of the latest message.</summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>When the thread record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the thread record was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Messages in this thread.</summary>
    public ICollection<EmailMessage> Messages { get; set; } = new List<EmailMessage>();
}
