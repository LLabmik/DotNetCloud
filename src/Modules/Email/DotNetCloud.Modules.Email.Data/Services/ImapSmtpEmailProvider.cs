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
    private readonly IAttachmentStorage _attachmentStorage;
    private readonly ILogger<ImapSmtpEmailProvider> _logger;

    public ImapSmtpEmailProvider(
        EmailDbContext db,
        EmailCredentialEncryptionService encryption,
        IEventBus eventBus,
        IAttachmentStorage attachmentStorage,
        ILogger<ImapSmtpEmailProvider> logger)
    {
        _db = db;
        _encryption = encryption;
        _eventBus = eventBus;
        _attachmentStorage = attachmentStorage;
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
                        try
                        {
                            var msg = await folder.GetMessageAsync(summary.UniqueId, ct);
                            var (_, htmlBody) = ExtractBodyFromMimeMessage(msg);
                            if (!string.IsNullOrWhiteSpace(htmlBody))
                            {
                                existing.BodyHtml = htmlBody;
                                existing.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch full message for body extraction UID={Uid}", summary.UniqueId.Id);
                        }
                    }

                    // Backfill attachment content for existing messages that were synced
                    // before attachment support was added (no records in EmailAttachments).
                    if (summary.Attachments is not null && summary.Attachments.Any())
                    {
                        var hasStoredAttachments = await _db.EmailAttachments
                            .AnyAsync(a => a.MessageId == existing.Id, ct);

                        if (!hasStoredAttachments)
                        {
                            try
                            {
                                var msg = await folder.GetMessageAsync(summary.UniqueId, ct);
                                await ProcessMimeMessageAttachmentsAsync(msg, existing, ct);
                                _logger.LogInformation("Backfilled attachment content for existing message {MessageId}", existing.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to backfill attachments for message {MessageId}", existing.Id);
                            }
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

                // Fetch full MIME message for body extraction and attachment content
                MimeMessage? fullMessage = null;
                try
                {
                    fullMessage = await folder.GetMessageAsync(summary.UniqueId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch full message UID={Uid}", summary.UniqueId.Id);
                }

                // Extract body from full message
                var (bodyPreview, bodyHtml) = ExtractBodyFromMimeMessage(fullMessage);

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

                // Merge new message participants into thread ParticipantsJson
                var currentParticipants = JsonSerializer.Deserialize<List<EmailAddressDto>>(thread.ParticipantsJson) ?? [];
                var fromAddresses = JsonSerializer.Deserialize<List<EmailAddressDto>>(emailMessage.FromJson) ?? [];
                var toAddresses = JsonSerializer.Deserialize<List<EmailAddressDto>>(emailMessage.ToJson) ?? [];
                List<EmailAddressDto> ccAddresses = [];
                if (!string.IsNullOrWhiteSpace(emailMessage.CcJson) && emailMessage.CcJson != "[]")
                    ccAddresses = JsonSerializer.Deserialize<List<EmailAddressDto>>(emailMessage.CcJson) ?? [];
                var mergedParticipants = currentParticipants
                    .Concat(fromAddresses).Concat(toAddresses).Concat(ccAddresses)
                    .DistinctBy(a => a.Email.ToLowerInvariant())
                    .ToList();
                thread.ParticipantsJson = JsonSerializer.Serialize(mergedParticipants);

                thread.MessageCount++;
                thread.LastMessageAt = emailMessage.DateReceived ?? emailMessage.CreatedAt;
                touchedThreadIds.Add(thread.Id);

                // Process attachments (download content and store via IAttachmentStorage)
                if (fullMessage is not null)
                {
                    await ProcessMimeMessageAttachmentsAsync(fullMessage, emailMessage, ct);
                }
                else
                {
                    // Fallback: store metadata only from summary
                    await ProcessAttachmentMetadataOnlyAsync(summary, emailMessage, ct);
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

        // Add attachments from request
        if (request.Attachments is { Count: > 0 })
        {
            foreach (var attRef in request.Attachments)
            {
                try
                {
                    var stream = await _attachmentStorage.OpenReadAsync(attRef.StorageKey, ct);
                    if (stream is null)
                    {
                        _logger.LogWarning("Attachment storage key not found: {StorageKey}, skipping", attRef.StorageKey);
                        continue;
                    }

                    await using (stream)
                    {
                        await using var memStream = new MemoryStream();
                        await stream.CopyToAsync(memStream, ct);
                        var bytes = memStream.ToArray();
                        var contentTypeObj = MimeKit.ContentType.Parse(attRef.ContentType);

                        if (attRef.IsInline && attRef.ContentId is not null)
                        {
                            builder.LinkedResources.Add(attRef.FileName, bytes, contentTypeObj);
                            builder.LinkedResources.Last().ContentId = attRef.ContentId;
                        }
                        else
                        {
                            builder.Attachments.Add(attRef.FileName, bytes, contentTypeObj);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to attach file {FileName} (key={StorageKey}), skipping", attRef.FileName, attRef.StorageKey);
                }
            }
        }

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

    private static (string? Preview, string? HtmlBody) ExtractBodyFromMimeMessage(MimeMessage? message)
    {
        if (message is null)
            return (null, null);

        try
        {
            var htmlBody = message.HtmlBody;

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

    private async Task ProcessMimeMessageAttachmentsAsync(MimeMessage message, EmailMessage emailMessage, CancellationToken ct)
    {
        // Process regular attachments
        foreach (var attachment in message.Attachments)
        {
            var fileName = attachment.ContentDisposition?.FileName
                ?? attachment.ContentType?.Name
                ?? "attachment";
            var contentType = attachment.ContentType?.MimeType ?? "application/octet-stream";
            var isInline = attachment.IsAttachment == false;

            try
            {
                await using var memStream = new MemoryStream();
                if (attachment is MimePart mimePart && mimePart.Content is not null)
                {
                    await mimePart.Content.DecodeToAsync(memStream, ct);
                }
                else if (attachment is MessagePart messagePart && messagePart.Message is not null)
                {
                    // Embedded message as attachment
                    await messagePart.Message.WriteToAsync(memStream, ct);
                }
                else
                {
                    continue;
                }

                memStream.Position = 0;
                var result = await _attachmentStorage.StoreAsync(memStream, fileName, contentType, ct);

                var emailAttachment = new EmailAttachment
                {
                    Message = emailMessage,
                    FileName = fileName,
                    ContentType = contentType,
                    Size = result.Size,
                    StorageKey = result.StorageKey,
                    ContentHash = result.ContentHash,
                    ContentId = attachment.ContentId,
                    CreatedAt = DateTime.UtcNow
                };

                _db.EmailAttachments.Add(emailAttachment);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process attachment {FileName} for message {MessageId}", fileName, emailMessage.Id);
            }
        }

        // Process inline/linked resources (images embedded in HTML, etc.)
        // MimeMessage.BodyParts gives us all MIME parts including those with ContentId
        foreach (var bodyPart in message.BodyParts)
        {
            if (bodyPart is MimePart mimePart && !string.IsNullOrWhiteSpace(mimePart.ContentId))
            {
                // Check if this was already added as an attachment
                if (_db.EmailAttachments.Local.Any(a => a.ContentId == mimePart.ContentId))
                    continue;

                var fileName = mimePart.ContentId ?? mimePart.ContentType?.Name ?? "inline-image";
                var contentType = mimePart.ContentType?.MimeType ?? "application/octet-stream";

                try
                {
                    await using var inlineStream = new MemoryStream();
                    if (mimePart.Content is null) continue;
                    await mimePart.Content.DecodeToAsync(inlineStream, ct);
                    inlineStream.Position = 0;
                    var result = await _attachmentStorage.StoreAsync(inlineStream, fileName, contentType, ct);

                    var emailAttachment = new EmailAttachment
                    {
                        Message = emailMessage,
                        FileName = fileName,
                        ContentType = contentType,
                        Size = result.Size,
                        StorageKey = result.StorageKey,
                        ContentHash = result.ContentHash,
                        ContentId = mimePart.ContentId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _db.EmailAttachments.Add(emailAttachment);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process inline resource {ContentId} for message {MessageId}", mimePart.ContentId, emailMessage.Id);
                }
            }
        }
    }

    private async Task ProcessAttachmentMetadataOnlyAsync(IMessageSummary summary, EmailMessage emailMessage, CancellationToken ct)
    {
        if (summary.Attachments is null)
            return;

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
