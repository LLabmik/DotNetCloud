using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;

namespace DotNetCloud.Modules.Email.Services;

/// <summary>
/// HTTP API client for Email REST endpoints.
/// </summary>
public interface IEmailApiClient
{
    // Accounts
    Task<IReadOnlyList<EmailAccount>> ListAccountsAsync(CancellationToken ct = default);
    Task<EmailAccount?> GetAccountAsync(Guid id, CancellationToken ct = default);
    Task<EmailAccount?> CreateAccountAsync(CreateEmailAccountRequest request, CancellationToken ct = default);
    Task<EmailAccount?> UpdateAccountAsync(Guid id, UpdateEmailAccountRequest request, CancellationToken ct = default);
    Task DeleteAccountAsync(Guid id, CancellationToken ct = default);

    // Mailboxes
    Task<IReadOnlyList<EmailMailbox>> ListMailboxesAsync(Guid accountId, CancellationToken ct = default);

    // Threads
    Task<IReadOnlyList<EmailThread>> ListThreadsAsync(Guid accountId, Guid mailboxId, CancellationToken ct = default);
    Task<IReadOnlyList<EmailMessage>> ListThreadMessagesAsync(Guid threadId, CancellationToken ct = default);

    // Messages
    Task<string?> GetMessageBodyAsync(Guid messageId, CancellationToken ct = default);

    // Send
    Task SendAsync(Guid accountId, EmailSendRequest request, CancellationToken ct = default);

    // Attachments
    /// <summary>Downloads an attachment and returns the stream with metadata.</summary>
    Task<(Stream Stream, string FileName, string ContentType)> DownloadAttachmentAsync(Guid attachmentId, CancellationToken ct = default);

    /// <summary>Uploads a file as a temp compose attachment. Returns storage metadata.</summary>
    Task<UploadAttachmentResult> UploadAttachmentAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>Detaches an attachment to the Files module.</summary>
    Task DetachAttachmentAsync(Guid attachmentId, Guid? targetFolderId = null, CancellationToken ct = default);

    // Sync
    Task TriggerSyncAsync(Guid accountId, CancellationToken ct = default);

    // Gmail OAuth status (the OAuth flow itself uses full page redirects, not API calls)
    Task<bool> CheckGmailOAuthConfiguredAsync(CancellationToken ct = default);

    // Rules
    Task<IReadOnlyList<EmailRule>> ListRulesAsync(Guid? accountId = null, CancellationToken ct = default);
    Task<EmailRule?> GetRuleAsync(Guid id, CancellationToken ct = default);
    Task<EmailRule?> CreateRuleAsync(CreateEmailRuleRequest request, CancellationToken ct = default);
    Task<EmailRule?> UpdateRuleAsync(Guid id, UpdateEmailRuleRequest request, CancellationToken ct = default);
    Task DeleteRuleAsync(Guid id, CancellationToken ct = default);
    Task<int> RunRulesAsync(Guid? accountId = null, Guid? mailboxId = null, CancellationToken ct = default);
}

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


