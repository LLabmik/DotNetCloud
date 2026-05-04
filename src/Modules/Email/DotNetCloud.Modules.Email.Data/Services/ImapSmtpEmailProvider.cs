using System.Globalization;
using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Email provider for IMAP (receive) + SMTP (send). Uses MailKit for protocol operations.
/// Handles full MIME message normalization into EmailThread/EmailMessage entities.
/// </summary>
public sealed class ImapSmtpEmailProvider : IEmailProvider
{
    private readonly EmailDbContext _db;
    private readonly EmailCredentialEncryptionService _encryption;
    private readonly IEventBus _eventBus;
    private readonly ILogger<ImapSmtpEmailProvider> _logger;

    public ImapSmtpEmailProvider(
        EmailDbContext db,
        EmailCredentialEncryptionService encryption,
        IEventBus eventBus,
        ILogger<ImapSmtpEmailProvider> logger)
    {
        _db = db;
        _encryption = encryption;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public EmailProviderType ProviderType => EmailProviderType.ImapSmtp;

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailMailbox>> ListMailboxesAsync(EmailAccount account, CancellationToken ct = default)
    {
        using var client = await ConnectImapAsync(account, ct);
        // Get folders: use personal namespace if available, otherwise root namespace
        var ns = client.PersonalNamespaces.Count > 0
            ? client.PersonalNamespaces[0]
            : new FolderNamespace('/', "");
        var folders = await client.GetFoldersAsync(ns, cancellationToken: ct);
        var mailboxes = folders.Select(f => MapFolderToMailbox(f, account.Id)).ToList();

        // INBOX is a special IMAP folder that may NOT be returned by GetFoldersAsync
        // on many servers (it's accessed via client.Inbox). Ensure it's included.
        var hasInbox = mailboxes.Any(m => m.ProviderId.Equals("INBOX", StringComparison.OrdinalIgnoreCase));
        if (!hasInbox)
        {
            var inboxFolder = client.Inbox;
            if (inboxFolder is not null)
            {
                mailboxes.Add(MapFolderToMailbox(inboxFolder, account.Id));
            }
        }

        return mailboxes;
    }

    /// <inheritdoc />
    public async Task<EmailSyncResult> SyncMailboxAsync(EmailAccount account, EmailMailbox mailbox, CancellationToken ct = default)
    {
        using var client = await ConnectImapAsync(account, ct);

        var folder = await client.GetFolderAsync(mailbox.ProviderId, ct);
        await folder.OpenAsync(FolderAccess.ReadWrite, ct);

        var syncState = DeserializeSyncState(account);
        var lastUid = syncState.GetValueOrDefault(mailbox.ProviderId, 0u);
        var result = new EmailSyncResult();
        var touchedThreadIds = new HashSet<Guid>();

        // Fetch all messages by index range (more reliable than UID search across servers).
        // If we have a watermark, only fetch messages newer than the last known UID by
        // searching UIDs above the watermark; otherwise fetch everything.
        UniqueIdSet uids;
        if (lastUid > 0)
        {
            var searchResult = await folder.SearchAsync(
                SearchQuery.Uids(new UniqueIdRange(new UniqueId(lastUid + 1), UniqueId.MaxValue)), ct);
            uids = new UniqueIdSet();
            foreach (var u in searchResult) uids.Add(u);
        }
        else
        {
            // First sync: fetch all messages by index, then convert to UIDs
            // This is more reliable than SearchQuery.All on some IMAP servers
            if (folder.Count == 0)
            {
                await folder.CloseAsync(cancellationToken: ct);
                return result;
            }

            var summaries = await folder.FetchAsync(0, -1,
                MessageSummaryItems.UniqueId, ct);
            uids = new UniqueIdSet();
            foreach (var s in summaries)
                uids.Add(s.UniqueId);
        }

        _logger.LogInformation("IMAP mailbox {Mailbox}: processing {Count} messages (lastUid={LastUid})",
            mailbox.ProviderId, uids.Count, lastUid);

        if (uids.Count == 0)
        {
            await folder.CloseAsync(cancellationToken: ct);
            return result;
        }

        // Fetch message summaries with headers needed for threading
        var fetchSummaries = await folder.FetchAsync(uids,
            MessageSummaryItems.UniqueId | MessageSummaryItems.Flags |
            MessageSummaryItems.InternalDate | MessageSummaryItems.Envelope |
            MessageSummaryItems.BodyStructure | MessageSummaryItems.GMailMessageId |
            MessageSummaryItems.GMailThreadId | MessageSummaryItems.Headers,
            ct);

        // Fetch full messages for body extraction (batch for efficiency)
        var fullMessages = await folder.FetchAsync(uids,
            MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure |
            MessageSummaryItems.Flags,
            ct);

        foreach (var summary in fetchSummaries.OrderBy(m => m.UniqueId.Id))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var messageId = summary.Envelope?.MessageId ?? summary.GMailMessageId?.ToString();
                if (string.IsNullOrWhiteSpace(messageId))
                    messageId = $"imap-{account.Id}-{summary.UniqueId.Id}";

                // Extract threading headers
                var inReplyTo = summary.Envelope?.InReplyTo;
                var references = GetHeaderValue(summary, "References") ?? summary.Envelope?.InReplyTo;

                // Find or create thread
                var thread = await FindOrCreateThreadAsync(account.Id, account.OwnerId, messageId,
                    inReplyTo, references, summary.Envelope?.Subject ?? "(No Subject)", ct);

                // Check if this message already exists in this mailbox
                // NOTE: UIDs are per-folder on IMAP, so we must scope by MailboxId.
                var existing = await _db.EmailMessages
                    .FirstOrDefaultAsync(m => m.AccountId == account.Id
                        && m.MailboxId == mailbox.Id
                        && m.ProviderMessageId == summary.UniqueId.Id.ToString(), ct);

                if (existing is not null)
                {
                    // Update flags
                    UpdateMessageFlags(existing, summary.Flags ?? MessageFlags.None);

                    // Populate BodyHtml if missing (e.g. from before migration)
                    if (string.IsNullOrWhiteSpace(existing.BodyHtml))
                    {
                        var (_, htmlBody) = await ExtractBodyAsync(folder, summary, client, ct);
                        if (!string.IsNullOrWhiteSpace(htmlBody))
                        {
                            existing.BodyHtml = htmlBody;
                            existing.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    result.UpdatedMessages++;
                    continue;
                }

                // Create message entity
                var from = summary.Envelope?.From.Mailboxes
                    .Select(m => new EmailAddressDto { Name = m.Name, Email = m.Address })
                    .ToList() ?? [];

                var to = summary.Envelope?.To.Mailboxes
                    .Select(m => new EmailAddressDto { Name = m.Name, Email = m.Address })
                    .ToList() ?? [];

                var cc = summary.Envelope?.Cc.Mailboxes
                    .Select(m => new EmailAddressDto { Name = m.Name, Email = m.Address })
                    .ToList() ?? [];

                // Extract body preview and full HTML body
                var (bodyPreview, bodyHtml) = await ExtractBodyAsync(folder, summary, client, ct);

                var emailMessage = new EmailMessage
                {
                    ThreadId = thread.Id,
                    AccountId = account.Id,
                    MailboxId = mailbox.Id,
                    ProviderMessageId = summary.UniqueId.Id.ToString(),
                    MessageIdHeader = messageId,
                    InReplyTo = inReplyTo,
                    References = references,
                    FromJson = JsonSerializer.Serialize(from),
                    ToJson = JsonSerializer.Serialize(to),
                    CcJson = cc.Count > 0 ? JsonSerializer.Serialize(cc) : "[]",
                    Subject = summary.Envelope?.Subject ?? "(No Subject)",
                    BodyPreview = bodyPreview,
                    BodyHtml = bodyHtml,
                    DateReceived = summary.InternalDate?.UtcDateTime ?? summary.Envelope?.Date?.UtcDateTime,
                    DateSent = summary.Envelope?.Date?.UtcDateTime,
                    IsRead = summary.Flags?.HasFlag(MessageFlags.Seen) ?? false,
                    IsStarred = summary.Flags?.HasFlag(MessageFlags.Flagged) ?? false,
                    FlagsJson = SerializeFlags(summary.Flags)
                };

                _db.EmailMessages.Add(emailMessage);
                thread.MessageCount++;
                thread.LastMessageAt = emailMessage.DateReceived ?? emailMessage.CreatedAt;
                touchedThreadIds.Add(thread.Id);

                // Process attachments
                if (summary.Attachments is not null)
                {
                    foreach (var attach in summary.Attachments)
                    {
                        if (attach is BodyPartBasic bp && !string.IsNullOrWhiteSpace(bp.ContentDescription))
                        {
                            _db.EmailAttachments.Add(new EmailAttachment
                            {
                                Message = emailMessage,
                                FileName = bp.ContentDescription ?? bp.PartSpecifier ?? "attachment",
                                ContentType = bp.ContentType?.MimeType ?? "application/octet-stream",
                                Size = bp.Octets > 0 ? (long)bp.Octets : 0
                            });
                        }
                    }
                }

                result.NewMessages++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to normalize message UID={Uid} in mailbox {Mailbox}",
                    summary.UniqueId.Id, mailbox.ProviderId);
            }
        }

        // Update sync watermark
        if (fetchSummaries.Count > 0)
        {
            syncState[mailbox.ProviderId] = fetchSummaries.Max(m => m.UniqueId.Id);
            result.SyncWatermark = JsonSerializer.Serialize(syncState);
        }

        // Detect deleted messages
        var deleted = await folder.SearchAsync(SearchQuery.Deleted, ct);
        if (deleted.Count > 0)
        {
            var deletedUids = deleted.Select(u => u.Id.ToString()).ToHashSet();
            var toMark = await _db.EmailMessages
                .Where(m => m.AccountId == account.Id
                    && m.MailboxId == mailbox.Id
                    && !m.IsDeleted
                    && deletedUids.Contains(m.ProviderMessageId))
                .ToListAsync(ct);

            foreach (var msg in toMark)
            {
                msg.IsDeleted = true;
                msg.UpdatedAt = DateTime.UtcNow;
            }
            result.DeletedMessages = toMark.Count;
        }

        await folder.CloseAsync(cancellationToken: ct);
        await _db.SaveChangesAsync(ct);

        // Fire search index events for touched threads
        foreach (var threadId in touchedThreadIds)
        {
            await _eventBus.PublishAsync(new SearchIndexRequestEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                ModuleId = "email",
                EntityId = threadId.ToString(),
                Action = SearchIndexAction.Index
            }, CallerContext.CreateSystemContext(), ct);
        }

        _logger.LogInformation("IMAP sync complete for mailbox {Mailbox}: {New} new, {Updated} updated, {Deleted} deleted",
            mailbox.ProviderId, result.NewMessages, result.UpdatedMessages, result.DeletedMessages);

        return result;
    }

    /// <inheritdoc />
    public async Task SendAsync(EmailAccount account, EmailSendRequest request, CancellationToken ct = default)
    {
        var creds = GetCredentials(account);

        _logger.LogInformation("SMTP Send: connecting to {Host}:{Port}, StartTls={StartTls}, Username={User}",
            creds.SmtpHost, creds.SmtpPort, creds.SmtpUseStartTls, creds.Username);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(account.DisplayName, account.EmailAddress));
        message.Subject = request.Subject;

        foreach (var recipient in request.To)
            message.To.Add(new MailboxAddress(recipient.Name ?? "", recipient.Email));

        if (request.Cc is not null)
            foreach (var cc in request.Cc)
                message.Cc.Add(new MailboxAddress(cc.Name ?? "", cc.Email));

        if (request.Bcc is not null)
            foreach (var bcc in request.Bcc)
                message.Bcc.Add(new MailboxAddress(bcc.Name ?? "", bcc.Email));

        var builder = new BodyBuilder();
        if (request.BodyHtml is not null)
            builder.HtmlBody = request.BodyHtml;
        if (request.BodyPlainText is not null)
            builder.TextBody = request.BodyPlainText;

        if (request.InReplyToMessageId is not null)
            message.InReplyTo = request.InReplyToMessageId;

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();

        _logger.LogInformation("SMTP Send: connecting...");
        await client.ConnectAsync(creds.SmtpHost, creds.SmtpPort,
            SecureSocketOptions.Auto, ct);
        _logger.LogInformation("SMTP Send: connected, authenticating...");
        await client.AuthenticateAsync(creds.Username, creds.Password, ct);
        _logger.LogInformation("SMTP Send: authenticated, sending message...");
        await client.SendAsync(message, ct);
        _logger.LogInformation("SMTP Send: message sent, disconnecting...");
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("Email sent via SMTP to {Recipients}", string.Join(", ", request.To.Select(t => t.Email)));
    }

    /// <inheritdoc />
    public async Task ApplyActionsAsync(EmailAccount account, IReadOnlyList<EmailAction> actions, CancellationToken ct = default)
    {
        using var client = await ConnectImapAsync(account, ct);

        foreach (var action in actions)
        {
            foreach (var msgId in action.MessageProviderIds)
            {
                if (!UniqueId.TryParse(msgId, out var uid)) continue;

                var inbox = client.Inbox
                    ?? throw new InvalidOperationException("IMAP INBOX not found.");
                await inbox.OpenAsync(FolderAccess.ReadWrite, ct);

                switch (action.ActionType)
                {
                    case EmailRuleActionType.MarkRead:
                        await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, ct);
                        break;
                    case EmailRuleActionType.MarkUnread:
                        await inbox.RemoveFlagsAsync(uid, MessageFlags.Seen, true, ct);
                        break;
                    case EmailRuleActionType.Star:
                    case EmailRuleActionType.Unstar:
                        await inbox.AddFlagsAsync(uid, MessageFlags.Flagged, true, ct);
                        break;
                    case EmailRuleActionType.MoveToFolder when action.TargetValue is not null:
                        var target = await client.GetFolderAsync(action.TargetValue, ct);
                        await inbox.MoveToAsync(uid, target, ct);
                        break;
                    case EmailRuleActionType.Archive:
                        var archive = client.GetFolder(SpecialFolder.Archive);
                        if (archive is not null)
                            await inbox.MoveToAsync(uid, archive, ct);
                        break;
                }
            }
        }

        await client.DisconnectAsync(true, ct);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateCredentialsAsync(EmailAccount account, CancellationToken ct = default)
    {
        try
        {
            using var client = await ConnectImapAsync(account, ct);
            await client.DisconnectAsync(true, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IMAP credential validation failed for account {AccountId}", account.Id);
            return false;
        }
    }

    private async Task<ImapClient> ConnectImapAsync(EmailAccount account, CancellationToken ct)
    {
        var creds = GetCredentials(account);
        var client = new ImapClient();
        await client.ConnectAsync(creds.ImapHost, creds.ImapPort,
            creds.ImapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(creds.Username, creds.Password, ct);
        return client;
    }

    private async Task<EmailThread> FindOrCreateThreadAsync(Guid accountId, Guid ownerId,
        string messageId, string? inReplyTo, string? references, string subject, CancellationToken ct)
    {
        // First, try to find existing thread by ProviderThreadId (unique per account)
        var existingThread = await _db.EmailThreads
            .FirstOrDefaultAsync(t => t.AccountId == accountId
                && t.ProviderThreadId == messageId, ct);

        if (existingThread is not null)
            return existingThread;

        // Try to find parent message by In-Reply-To or References
        var parentMessageId = inReplyTo ?? references?.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        if (parentMessageId is not null)
        {
            var parentMessage = await _db.EmailMessages
                .FirstOrDefaultAsync(m => m.AccountId == accountId
                    && m.MessageIdHeader == parentMessageId, ct);

            if (parentMessage is not null)
            {
                var parentThread = await _db.EmailThreads
                    .FirstOrDefaultAsync(t => t.Id == parentMessage.ThreadId, ct);
                if (parentThread is not null)
                    return parentThread;
            }

            // Check if a thread already references this parent by References
            var refThread = await _db.EmailThreads
                .FirstOrDefaultAsync(t => t.AccountId == accountId
                    && t.Subject == subject
                    && t.LastMessageAt >= DateTime.UtcNow.AddDays(-30), ct);
            if (refThread is not null)
                return refThread;
        }

        // Create new thread
        var thread = new EmailThread
        {
            AccountId = accountId,
            Subject = subject,
            ProviderThreadId = messageId,
            ParticipantsJson = "[]",
            MessageCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.EmailThreads.Add(thread);
        return thread;
    }

    private static async Task<(string? Preview, string? HtmlBody)> ExtractBodyAsync(IMailFolder folder, IMessageSummary summary,
        ImapClient client, CancellationToken ct)
    {
        try
        {
            // Full message fetch gives us MimeKit-decoded content (handles
            // quoted-printable, base64, charset conversion automatically)
            var message = await folder.GetMessageAsync(summary.UniqueId, ct);

            // Capture full HTML body (rich content, formatting, images)
            var htmlBody = message.HtmlBody;

            // Build preview: prefer plain text; fall back to stripped HTML
            var text = message.TextBody;
            string? preview;
            if (!string.IsNullOrWhiteSpace(text))
            {
                preview = text.Length <= 500 ? text.Trim() : text[..500].Trim();
            }
            else if (!string.IsNullOrWhiteSpace(htmlBody))
            {
                var stripped = System.Text.RegularExpressions.Regex.Replace(htmlBody, "<[^>]+>", " ");
                stripped = System.Net.WebUtility.HtmlDecode(stripped);
                preview = stripped.Length <= 500 ? stripped.Trim() : stripped[..500].Trim();
            }
            else
            {
                preview = null;
            }

            return (preview, htmlBody);
        }
        catch (Exception)
        {
            return (null, null);
        }
    }

    private static BodyPartBasic? FindFirstTextPart(BodyPartMultipart parent, string mimeType)
    {
        foreach (var part in parent.BodyParts)
        {
            if (part is BodyPartBasic bp && bp.ContentType?.MimeType?.StartsWith(mimeType, StringComparison.OrdinalIgnoreCase) == true)
                return bp;
            if (part is BodyPartMultipart nested)
            {
                var found = FindFirstTextPart(nested, mimeType);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static void UpdateMessageFlags(EmailMessage message, MessageFlags flags)
    {
        message.IsRead = flags.HasFlag(MessageFlags.Seen);
        message.IsStarred = flags.HasFlag(MessageFlags.Flagged);
        message.FlagsJson = SerializeFlags(flags);
        message.UpdatedAt = DateTime.UtcNow;
    }

    private static string? GetHeaderValue(IMessageSummary summary, string headerName)
    {
        if (summary.Headers is null) return null;
        var header = summary.Headers.FirstOrDefault(h =>
            h.Field.Equals(headerName, StringComparison.OrdinalIgnoreCase));
        return header?.Value;
    }

    private static string SerializeFlags(MessageFlags? flags)
    {
        if (flags is null) return "[]";
        var list = new List<string>();
        if (flags.Value.HasFlag(MessageFlags.Seen)) list.Add("\\Seen");
        if (flags.Value.HasFlag(MessageFlags.Flagged)) list.Add("\\Flagged");
        if (flags.Value.HasFlag(MessageFlags.Answered)) list.Add("\\Answered");
        if (flags.Value.HasFlag(MessageFlags.Draft)) list.Add("\\Draft");
        if (flags.Value.HasFlag(MessageFlags.Deleted)) list.Add("\\Deleted");
        return JsonSerializer.Serialize(list);
    }

    private static Dictionary<string, uint> DeserializeSyncState(EmailAccount account)
    {
        if (string.IsNullOrEmpty(account.SyncStateJson))
            return [];
        return JsonSerializer.Deserialize<Dictionary<string, uint>>(account.SyncStateJson) ?? [];
    }

    private static readonly JsonSerializerOptions _credentialSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private ImapSmtpCredentials GetCredentials(EmailAccount account)
    {
        if (account.EncryptedCredentialBlob is null)
            throw new InvalidOperationException($"No credentials stored for account {account.Id}.");

        var bytes = _encryption.Unprotect(account.EncryptedCredentialBlob, account.OwnerId);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<ImapSmtpCredentials>(json, _credentialSerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize IMAP/SMTP credentials.");
    }

    private static EmailMailbox MapFolderToMailbox(IMailFolder folder, Guid accountId)
    {
        var flags = MailboxFlags.None;
        var name = folder.FullName;
        if (name.Equals("INBOX", StringComparison.OrdinalIgnoreCase)) flags |= MailboxFlags.Inbox;
        if (folder.Attributes.HasFlag(FolderAttributes.Sent)) flags |= MailboxFlags.Sent;
        if (folder.Attributes.HasFlag(FolderAttributes.Trash)) flags |= MailboxFlags.Trash;
        if (folder.Attributes.HasFlag(FolderAttributes.Drafts)) flags |= MailboxFlags.Drafts;
        if (folder.Attributes.HasFlag(FolderAttributes.Archive)) flags |= MailboxFlags.Archive;
        if (folder.Attributes.HasFlag(FolderAttributes.Junk)) flags |= MailboxFlags.Spam;

        return new EmailMailbox
        {
            AccountId = accountId,
            ProviderId = folder.FullName,
            DisplayName = folder.Name ?? folder.FullName,
            SyncFlags = (int)flags
        };
    }
}

internal sealed record ImapSmtpCredentials
{
    public required string ImapHost { get; init; }
    public int ImapPort { get; init; } = 993;
    public bool ImapUseSsl { get; init; } = true;
    public required string SmtpHost { get; init; }
    public int SmtpPort { get; init; } = 587;
    public bool SmtpUseStartTls { get; init; } = true;
    public required string Username { get; init; }
    public required string Password { get; init; }
}
