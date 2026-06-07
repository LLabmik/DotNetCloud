namespace DotNetCloud.Core.Services.ModuleApis;

/// <summary>
/// gRPC API client interface for the Email module.
/// </summary>
public interface IEmailApiClient
{
    // Accounts
    Task<IReadOnlyList<EmailAccountDto>> ListAccountsAsync(CancellationToken ct = default);
    Task<EmailAccountDto?> GetAccountAsync(Guid id, CancellationToken ct = default);
    Task<EmailAccountDto?> CreateAccountAsync(CreateEmailAccountRequest request, CancellationToken ct = default);
    Task<EmailAccountDto?> UpdateAccountAsync(Guid id, UpdateEmailAccountRequest request, CancellationToken ct = default);
    Task DeleteAccountAsync(Guid id, CancellationToken ct = default);

    // Mailboxes
    Task<IReadOnlyList<EmailMailboxDto>> ListMailboxesAsync(Guid accountId, CancellationToken ct = default);

    // Threads
    Task<IReadOnlyList<EmailThreadDto>> ListThreadsAsync(Guid accountId, Guid mailboxId, CancellationToken ct = default);
    Task<IReadOnlyList<EmailMessageDto>> ListThreadMessagesAsync(Guid threadId, CancellationToken ct = default);

    // Messages
    Task<string?> GetMessageBodyAsync(Guid messageId, CancellationToken ct = default);

    // Send
    Task SendAsync(Guid accountId, EmailSendRequest request, CancellationToken ct = default);

    // Attachments
    Task<(Stream Stream, string FileName, string ContentType)> DownloadAttachmentAsync(Guid attachmentId, CancellationToken ct = default);
    Task<UploadAttachmentResult> UploadAttachmentAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);
    Task DetachAttachmentAsync(Guid attachmentId, Guid? targetFolderId = null, CancellationToken ct = default);
    Task<UploadAttachmentResult> AttachFromFilesModuleAsync(Guid fileNodeId, CancellationToken ct = default);

    // Sync
    Task TriggerSyncAsync(Guid accountId, CancellationToken ct = default);

    // Gmail OAuth status
    Task<bool> CheckGmailOAuthConfiguredAsync(CancellationToken ct = default);

    // Rules
    Task<IReadOnlyList<EmailRuleDto>> ListRulesAsync(Guid? accountId = null, CancellationToken ct = default);
    Task<EmailRuleDto?> GetRuleAsync(Guid id, CancellationToken ct = default);
    Task<EmailRuleDto?> CreateRuleAsync(CreateEmailRuleRequest request, CancellationToken ct = default);
    Task<EmailRuleDto?> UpdateRuleAsync(Guid id, UpdateEmailRuleRequest request, CancellationToken ct = default);
    Task DeleteRuleAsync(Guid id, CancellationToken ct = default);
    Task<int> RunRulesAsync(Guid? accountId = null, Guid? mailboxId = null, CancellationToken ct = default);
}

// ─── Account DTOs ───────────────────────────────────────────────────────

/// <summary>Flat DTO for an email account, without EF navigation properties.</summary>
public sealed record EmailAccountDto
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>The user who owns this account.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>Email provider type (ImapSmtp, GmailOAuth, Exchange, etc.).</summary>
    public string ProviderType { get; init; } = "";

    /// <summary>User-visible account name.</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>The email address for this account.</summary>
    public string EmailAddress { get; init; } = "";

    /// <summary>Whether the account is enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>When the account was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the account was last updated.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Request DTO for creating an email account.</summary>
public sealed record CreateEmailAccountRequest
{
    /// <summary>Email provider type.</summary>
    public required string ProviderType { get; init; }

    /// <summary>User-visible account name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>The email address.</summary>
    public required string EmailAddress { get; init; }

    /// <summary>Encrypted credentials JSON.</summary>
    public required string CredentialsJson { get; init; }
}

/// <summary>Request DTO for updating an email account.</summary>
public sealed record UpdateEmailAccountRequest
{
    /// <summary>Updated display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Whether the account is enabled.</summary>
    public bool? IsEnabled { get; init; }
}

// ─── Mailbox DTOs ──────────────────────────────────────────────────────

/// <summary>Flat DTO for a mailbox, without EF navigation properties.</summary>
public sealed record EmailMailboxDto
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Parent account ID.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Provider-side mailbox ID.</summary>
    public string ProviderId { get; init; } = "";

    /// <summary>Display name.</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>Mailbox flags (Inbox, Sent, Trash, etc.).</summary>
    public string SyncFlags { get; init; } = "";

    /// <summary>Sort order within the account.</summary>
    public int SortOrder { get; init; }

    /// <summary>When the mailbox was last synced.</summary>
    public DateTime? LastSyncedAt { get; init; }

    /// <summary>When the mailbox was created.</summary>
    public DateTime CreatedAt { get; init; }
}

// ─── Thread DTOs ───────────────────────────────────────────────────────

/// <summary>Flat DTO for an email thread, without EF navigation properties.</summary>
public sealed record EmailThreadDto
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Parent account ID.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Provider-side thread ID.</summary>
    public string ProviderThreadId { get; init; } = "";

    /// <summary>Thread subject.</summary>
    public string Subject { get; init; } = "";

    /// <summary>Latest snippet.</summary>
    public string Snippet { get; init; } = "";

    /// <summary>Number of messages in the thread.</summary>
    public int MessageCount { get; init; }

    /// <summary>Date of the last message.</summary>
    public DateTime? LastMessageAt { get; init; }

    /// <summary>When the thread was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the thread was last updated.</summary>
    public DateTime UpdatedAt { get; init; }
}

// ─── Message DTOs ──────────────────────────────────────────────────────

/// <summary>Flat DTO for an email message, without EF navigation properties.</summary>
public sealed record EmailMessageDto
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Parent thread ID.</summary>
    public Guid ThreadId { get; init; }

    /// <summary>Parent account ID.</summary>
    public Guid AccountId { get; init; }

    /// <summary>Mailbox ID.</summary>
    public Guid MailboxId { get; init; }

    /// <summary>Provider message ID.</summary>
    public string ProviderMessageId { get; init; } = "";

    /// <summary>Message-ID header value.</summary>
    public string MessageIdHeader { get; init; } = "";

    /// <summary>In-Reply-To header.</summary>
    public string? InReplyTo { get; init; }

    /// <summary>Subject.</summary>
    public string Subject { get; init; } = "";

    /// <summary>Body preview text.</summary>
    public string BodyPreview { get; init; } = "";

    /// <summary>Date received.</summary>
    public DateTime? DateReceived { get; init; }

    /// <summary>Date sent.</summary>
    public DateTime DateSent { get; init; }

    /// <summary>Whether the message has been read.</summary>
    public bool IsRead { get; init; }

    /// <summary>Whether the message is starred.</summary>
    public bool IsStarred { get; init; }

    /// <summary>Whether the message is deleted.</summary>
    public bool IsDeleted { get; init; }

    /// <summary>When the message was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the message was last updated.</summary>
    public DateTime UpdatedAt { get; init; }
}

// ─── Send DTOs ─────────────────────────────────────────────────────────

/// <summary>Request DTO for sending an email.</summary>
public sealed record EmailSendRequest
{
    /// <summary>Recipients (To).</summary>
    public required IReadOnlyList<EmailAddressDto> To { get; init; }

    /// <summary>CC recipients.</summary>
    public IReadOnlyList<EmailAddressDto>? Cc { get; init; }

    /// <summary>BCC recipients.</summary>
    public IReadOnlyList<EmailAddressDto>? Bcc { get; init; }

    /// <summary>Email subject.</summary>
    public required string Subject { get; init; }

    /// <summary>HTML body.</summary>
    public string? BodyHtml { get; init; }

    /// <summary>Plain text body.</summary>
    public string? BodyPlainText { get; init; }

    /// <summary>Message ID this is a reply to.</summary>
    public string? InReplyToMessageId { get; init; }

    /// <summary>References header values.</summary>
    public IReadOnlyList<string>? References { get; init; }

    /// <summary>Attachments.</summary>
    public IReadOnlyList<EmailAttachmentRef>? Attachments { get; init; }
}

/// <summary>Email address with optional display name.</summary>
public sealed record EmailAddressDto
{
    /// <summary>Display name.</summary>
    public string? Name { get; init; }

    /// <summary>Email address.</summary>
    public required string Email { get; init; }
}

/// <summary>Reference to an uploaded attachment.</summary>
public sealed record EmailAttachmentRef
{
    /// <summary>Storage key.</summary>
    public required string StorageKey { get; init; }

    /// <summary>File name.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME content type.</summary>
    public required string ContentType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Content-ID for inline images.</summary>
    public string? ContentId { get; init; }

    /// <summary>Whether the attachment is inline.</summary>
    public bool IsInline { get; init; }
}

// ─── Upload Result ─────────────────────────────────────────────────────

/// <summary>Result of uploading a temp compose attachment.</summary>
public sealed record UploadAttachmentResult
{
    /// <summary>Storage key for later reference in EmailSendRequest.</summary>
    public required string StorageKey { get; init; }

    /// <summary>Original filename.</summary>
    public required string FileName { get; init; }

    /// <summary>MIME content type.</summary>
    public required string ContentType { get; init; }

    /// <summary>File size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>SHA-256 content hash.</summary>
    public required string ContentHash { get; init; }
}

// ─── Rule DTOs ─────────────────────────────────────────────────────────

/// <summary>Flat DTO for an email rule.</summary>
public sealed record EmailRuleDto
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Owner user ID.</summary>
    public Guid OwnerId { get; init; }

    /// <summary>Optional account-specific rule.</summary>
    public Guid? AccountId { get; init; }

    /// <summary>Rule name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Whether the rule is enabled.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Priority (lower = higher priority).</summary>
    public int Priority { get; init; }

    /// <summary>Whether to stop processing after this rule matches.</summary>
    public bool StopProcessing { get; init; }

    /// <summary>When the rule was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>When the rule was last updated.</summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>Request DTO for creating an email rule.</summary>
public sealed record CreateEmailRuleRequest
{
    /// <summary>Rule name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional account ID to scope this rule.</summary>
    public Guid? AccountId { get; init; }

    /// <summary>Whether the rule is enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Priority.</summary>
    public int Priority { get; init; }

    /// <summary>Whether to stop processing after this rule matches.</summary>
    public bool StopProcessing { get; init; }

    /// <summary>Condition groups (OR-logic between groups).</summary>
    public required IReadOnlyList<CreateConditionGroupRequest> ConditionGroups { get; init; }

    /// <summary>Actions to execute when conditions match.</summary>
    public required IReadOnlyList<CreateRuleActionRequest> Actions { get; init; }
}

/// <summary>Request DTO for creating a condition group.</summary>
public sealed record CreateConditionGroupRequest
{
    /// <summary>Match mode (All = AND, Any = OR).</summary>
    public string MatchMode { get; init; } = "All";

    /// <summary>Conditions in this group.</summary>
    public required IReadOnlyList<CreateConditionRequest> Conditions { get; init; }
}

/// <summary>Request DTO for creating a condition.</summary>
public sealed record CreateConditionRequest
{
    /// <summary>Field to evaluate.</summary>
    public required string Field { get; init; }

    /// <summary>Operator.</summary>
    public required string Operator { get; init; }

    /// <summary>Value to compare against.</summary>
    public required string Value { get; init; }
}

/// <summary>Request DTO for creating a rule action.</summary>
public sealed record CreateRuleActionRequest
{
    /// <summary>Action type.</summary>
    public required string ActionType { get; init; }

    /// <summary>Target value (e.g., folder name).</summary>
    public string? TargetValue { get; init; }
}

/// <summary>Request DTO for updating an email rule.</summary>
public sealed record UpdateEmailRuleRequest
{
    /// <summary>Updated name.</summary>
    public string? Name { get; init; }

    /// <summary>Updated enabled state.</summary>
    public bool? IsEnabled { get; init; }

    /// <summary>Updated priority.</summary>
    public int? Priority { get; init; }

    /// <summary>Updated stop-processing flag.</summary>
    public bool? StopProcessing { get; init; }

    /// <summary>Replacement condition groups.</summary>
    public IReadOnlyList<CreateConditionGroupRequest>? ConditionGroups { get; init; }

    /// <summary>Replacement actions.</summary>
    public IReadOnlyList<CreateRuleActionRequest>? Actions { get; init; }
}
