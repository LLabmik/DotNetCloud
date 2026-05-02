using DotNetCloud.Modules.Email.Models;

namespace DotNetCloud.Modules.Email.Services;

/// <summary>
/// Abstract provider interface for email operations (IMAP/SMTP or Gmail API).
/// </summary>
public interface IEmailProvider
{
    /// <summary>The provider type this implementation handles.</summary>
    EmailProviderType ProviderType { get; }

    /// <summary>Lists mailboxes/labels for the account.</summary>
    Task<IReadOnlyList<EmailMailbox>> ListMailboxesAsync(EmailAccount account, CancellationToken ct = default);

    /// <summary>Syncs messages from a mailbox into the local database.</summary>
    Task<EmailSyncResult> SyncMailboxAsync(EmailAccount account, EmailMailbox mailbox, CancellationToken ct = default);

    /// <summary>Sends an email.</summary>
    Task SendAsync(EmailAccount account, EmailSendRequest request, CancellationToken ct = default);

    /// <summary>Applies actions to messages (mark read, move, label, etc.).</summary>
    Task ApplyActionsAsync(EmailAccount account, IReadOnlyList<EmailAction> actions, CancellationToken ct = default);

    /// <summary>Validates that the account credentials are correct.</summary>
    Task<bool> ValidateCredentialsAsync(EmailAccount account, CancellationToken ct = default);
}

/// <summary>Result of a sync operation.</summary>
public sealed record EmailSyncResult
{
    /// <summary>Number of new messages synced.</summary>
    public int NewMessages { get; set; }

    /// <summary>Number of messages updated (flags, labels, etc.).</summary>
    public int UpdatedMessages { get; set; }

    /// <summary>Number of messages deleted (expunged from provider).</summary>
    public int DeletedMessages { get; set; }

    /// <summary>Watermark/cursor for the next incremental sync.</summary>
    public string? SyncWatermark { get; set; }
}

/// <summary>Request to send an email.</summary>
public sealed record EmailSendRequest
{
    /// <summary>Recipients.</summary>
    public required IReadOnlyList<EmailAddressDto> To { get; init; }

    /// <summary>CC recipients.</summary>
    public IReadOnlyList<EmailAddressDto>? Cc { get; init; }

    /// <summary>BCC recipients.</summary>
    public IReadOnlyList<EmailAddressDto>? Bcc { get; init; }

    /// <summary>Message subject.</summary>
    public required string Subject { get; init; }

    /// <summary>HTML body.</summary>
    public string? BodyHtml { get; init; }

    /// <summary>Plain text body.</summary>
    public string? BodyPlainText { get; init; }

    /// <summary>In-Reply-To message ID header (for threading).</summary>
    public string? InReplyToMessageId { get; init; }

    /// <summary>References header values (for threading).</summary>
    public IReadOnlyList<string>? References { get; init; }
}

/// <summary>An action to apply to one or more messages.</summary>
public sealed record EmailAction
{
    /// <summary>Type of action.</summary>
    public required EmailRuleActionType ActionType { get; init; }

    /// <summary>Target message provider IDs.</summary>
    public required IReadOnlyList<string> MessageProviderIds { get; init; }

    /// <summary>Optional target value (mailbox name, label, etc.).</summary>
    public string? TargetValue { get; init; }
}

/// <summary>A simple email address DTO.</summary>
public sealed record EmailAddressDto
{
    /// <summary>Display name.</summary>
    public string? Name { get; init; }

    /// <summary>Email address.</summary>
    public required string Email { get; init; }
}
