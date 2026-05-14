using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Text;
using GmailMessagePart = Google.Apis.Gmail.v1.Data.MessagePart;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Email provider implementation for Gmail API (OAuth-based).
/// Handles full MIME message normalization into EmailThread/EmailMessage entities.
/// </summary>
public sealed class GmailEmailProvider : IEmailProvider
{
    private readonly EmailDbContext _db;
    private readonly EmailCredentialEncryptionService _encryption;
    private readonly IEventBus _eventBus;
    private readonly IAttachmentStorage _attachmentStorage;
    private readonly ILogger<GmailEmailProvider> _logger;

    public GmailEmailProvider(
        EmailDbContext db,
        EmailCredentialEncryptionService encryption,
        IEventBus eventBus,
        IAttachmentStorage attachmentStorage,
        ILogger<GmailEmailProvider> logger)
    {
        _db = db;
        _encryption = encryption;
        _eventBus = eventBus;
        _attachmentStorage = attachmentStorage;
        _logger = logger;
    }

    /// <inheritdoc />
    public EmailProviderType ProviderType => EmailProviderType.Gmail;

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailMailbox>> ListMailboxesAsync(EmailAccount account, CancellationToken ct = default)
    {
        var service = await CreateGmailServiceAsync(account, ct);
        var labelsReq = service.Users.Labels.List("me");
        var labels = await labelsReq.ExecuteAsync(ct);

        return labels.Labels?.Select(l => MapLabelToMailbox(l, account.Id)).ToList()
            ?? (IReadOnlyList<EmailMailbox>)Array.Empty<EmailMailbox>();
    }

    /// <inheritdoc />
    public async Task<EmailSyncResult> SyncMailboxAsync(EmailAccount account, EmailMailbox mailbox, CancellationToken ct = default)
    {
        var service = await CreateGmailServiceAsync(account, ct);
        var result = new EmailSyncResult();
        var touchedThreadIds = new HashSet<Guid>();

        // Parse sync state for incremental sync watermark (Unix timestamp seconds)
        var syncState = DeserializeSyncState(account);
        var lastSyncTime = syncState.GetValueOrDefault(mailbox.ProviderId, 0UL);

        // List messages for this label
        var listRequest = service.Users.Messages.List("me");
        listRequest.LabelIds = new[] { mailbox.ProviderId };
        listRequest.MaxResults = 100;

        // Incremental sync: only fetch messages newer than last sync
        if (lastSyncTime > 0)
        {
            listRequest.Q = $"after:{lastSyncTime}";
        }

        var listResponse = await listRequest.ExecuteAsync(ct);

        if (listResponse.Messages is null || listResponse.Messages.Count == 0)
            return result;

        // Fetch full message details for each message
        foreach (var msgRef in listResponse.Messages.OrderBy(m =>
            long.TryParse(m.Id, out var id) ? id : 0))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                // Check if already synced
                var existing = await _db.EmailMessages
                    .FirstOrDefaultAsync(m => m.AccountId == account.Id
                        && m.ProviderMessageId == msgRef.Id, ct);

                // Get full message with payload (used for both backfill and new messages)
                var getReq = service.Users.Messages.Get("me", msgRef.Id);
                getReq.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;

                if (existing is not null)
                {
                    // Backfill attachment content for existing messages that were synced
                    // before attachment support was added (no records in EmailAttachments).
                    var fullMsg = await getReq.ExecuteAsync(ct);
                    if (fullMsg?.Payload?.Parts is not null)
                    {
                        var hasMsgAttachments = fullMsg.Payload.Parts
                            .Any(p => !string.IsNullOrWhiteSpace(p.Filename));
                        var hasStoredAttachments = await _db.EmailAttachments
                            .AnyAsync(a => a.MessageId == existing.Id, ct);

                        if (hasMsgAttachments && !hasStoredAttachments)
                        {
                            try
                            {
                                await ProcessGmailAttachmentsAsync(service, fullMsg, existing, ct);
                                _logger.LogInformation("Backfilled attachment content for existing Gmail message {MessageId}", existing.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to backfill attachments for Gmail message {MessageId}", existing.Id);
                            }
                        }
                    }

                    result.UpdatedMessages++;
                    continue;
                }

                var message = await getReq.ExecuteAsync(ct);

                if (message?.Payload is null)
                    continue;

                // Extract headers
                var headers = message.Payload.Headers?
                    .ToDictionary(h => h.Name ?? "", h => h.Value ?? "", StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                headers.TryGetValue("Message-Id", out var messageId);
                headers.TryGetValue("In-Reply-To", out var inReplyTo);
                headers.TryGetValue("References", out var references);
                headers.TryGetValue("Subject", out var subject);
                headers.TryGetValue("From", out var fromHeader);
                headers.TryGetValue("To", out var toHeader);
                headers.TryGetValue("Cc", out var ccHeader);

                if (string.IsNullOrWhiteSpace(messageId))
                    messageId = $"gmail-{account.Id}-{msgRef.Id}";

                subject ??= "(No Subject)";

                // Find or create thread (use Gmail thread ID as provider thread key)
                var gmailThreadId = message.ThreadId ?? messageId;
                var thread = await FindOrCreateThreadAsync(account.Id, account.OwnerId,
                    gmailThreadId, messageId, inReplyTo, references, subject, ct);

                // Parse From/To/Cc
                var from = ParseAddressHeader(fromHeader);
                var to = ParseAddressHeader(toHeader);
                var cc = ParseAddressHeader(ccHeader);

                // Extract body preview from payload
                var bodyPreview = ExtractBodyPreview(message.Payload);

                // Serialize labels as flags
                var flags = SerializeGmailLabels(message.LabelIds);

                var emailMessage = new EmailMessage
                {
                    ThreadId = thread.Id,
                    AccountId = account.Id,
                    MailboxId = mailbox.Id,
                    ProviderMessageId = msgRef.Id,
                    MessageIdHeader = messageId,
                    InReplyTo = inReplyTo,
                    References = references,
                    FromJson = JsonSerializer.Serialize(from),
                    ToJson = JsonSerializer.Serialize(to),
                    CcJson = cc.Count > 0 ? JsonSerializer.Serialize(cc) : "[]",
                    Subject = subject,
                    BodyPreview = bodyPreview,
                    DateReceived = ParseInternalDate(message.InternalDate),
                    DateSent = ParseInternalDate(message.InternalDate),
                    IsRead = message.LabelIds?.Contains("UNREAD", StringComparer.OrdinalIgnoreCase) == false,
                    IsStarred = message.LabelIds?.Contains("STARRED", StringComparer.OrdinalIgnoreCase) == true,
                    FlagsJson = flags
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

                // Process attachments (download content via Gmail API)
                if (message.Payload.Parts is not null)
                {
                    await ProcessGmailAttachmentsAsync(service, message, emailMessage, ct);
                }

                result.NewMessages++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to normalize Gmail message {MsgId} for account {AccountId}",
                    msgRef.Id, account.Id);
            }
        }

        // Update sync watermark with current Unix timestamp
        syncState[mailbox.ProviderId] = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        result.SyncWatermark = JsonSerializer.Serialize(syncState);

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

        _logger.LogInformation("Gmail sync complete for mailbox {Mailbox}: {New} new, {Updated} updated",
            mailbox.ProviderId, result.NewMessages, result.UpdatedMessages);

        return result;
    }

    /// <inheritdoc />
    public async Task SendAsync(EmailAccount account, EmailSendRequest request, CancellationToken ct = default)
    {
        var service = await CreateGmailServiceAsync(account, ct);

        // Build MIME message using MimeKit for proper multipart support with attachments
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

        if (request.InReplyToMessageId is not null)
            message.InReplyTo = request.InReplyToMessageId;

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
                        await using var readStream = new MemoryStream();
                        await stream.CopyToAsync(readStream, ct);
                        var bytes = readStream.ToArray();
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

        message.Body = builder.ToMessageBody();

        // Encode MIME message to base64url for Gmail API
        await using var memStream = new MemoryStream();
        await message.WriteToAsync(memStream, ct);
        var raw = Convert.ToBase64String(memStream.ToArray())
            .Replace('+', '-').Replace('/', '_').Replace("=", "");

        var gmailMessage = new Message { Raw = raw };

        var sendReq = service.Users.Messages.Send(gmailMessage, "me");
        await sendReq.ExecuteAsync(ct);

        _logger.LogInformation("Email sent via Gmail API to {Recipients}", string.Join(", ", request.To.Select(t => t.Email)));
    }

    /// <inheritdoc />
    public async Task ApplyActionsAsync(EmailAccount account, IReadOnlyList<EmailAction> actions, CancellationToken ct = default)
    {
        var service = await CreateGmailServiceAsync(account, ct);

        foreach (var action in actions)
        {
            foreach (var msgId in action.MessageProviderIds)
            {
                var modifyReq = new ModifyMessageRequest();

                switch (action.ActionType)
                {
                    case EmailRuleActionType.MarkRead:
                        modifyReq.RemoveLabelIds = new List<string> { "UNREAD" };
                        break;
                    case EmailRuleActionType.MarkUnread:
                        modifyReq.AddLabelIds = new List<string> { "UNREAD" };
                        break;
                    case EmailRuleActionType.Star:
                        modifyReq.AddLabelIds = new List<string> { "STARRED" };
                        break;
                    case EmailRuleActionType.Unstar:
                        modifyReq.RemoveLabelIds = new List<string> { "STARRED" };
                        break;
                    case EmailRuleActionType.ApplyLabel when action.TargetValue is not null:
                        modifyReq.AddLabelIds = new List<string> { action.TargetValue };
                        break;
                    case EmailRuleActionType.Archive:
                        modifyReq.RemoveLabelIds = new List<string> { "INBOX" };
                        break;
                    case EmailRuleActionType.MoveToFolder when action.TargetValue is not null:
                        modifyReq.AddLabelIds = new List<string> { action.TargetValue };
                        modifyReq.RemoveLabelIds = new List<string> { "INBOX" };
                        break;
                }

                if (modifyReq.AddLabelIds is { Count: > 0 } || modifyReq.RemoveLabelIds is { Count: > 0 })
                {
                    var modify = service.Users.Messages.Modify(modifyReq, "me", msgId);
                    await modify.ExecuteAsync(ct);
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> ValidateCredentialsAsync(EmailAccount account, CancellationToken ct = default)
    {
        try
        {
            var service = await CreateGmailServiceAsync(account, ct);
            var profileReq = service.Users.GetProfile("me");
            await profileReq.ExecuteAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gmail credential validation failed for account {AccountId}", account.Id);
            return false;
        }
    }

    private async Task<EmailThread> FindOrCreateThreadAsync(Guid accountId, Guid ownerId,
        string gmailThreadId, string messageId, string? inReplyTo, string? references,
        string subject, CancellationToken ct)
    {
        // First, try to find existing thread by Gmail thread ID
        var existingThread = await _db.EmailThreads
            .FirstOrDefaultAsync(t => t.AccountId == accountId
                && t.ProviderThreadId == gmailThreadId, ct);

        if (existingThread is not null)
            return existingThread;

        // Try to find parent by In-Reply-To
        if (inReplyTo is not null)
        {
            var parentMessage = await _db.EmailMessages
                .FirstOrDefaultAsync(m => m.AccountId == accountId
                    && m.MessageIdHeader == inReplyTo, ct);

            if (parentMessage is not null)
            {
                var parentThread = await _db.EmailThreads
                    .FirstOrDefaultAsync(t => t.Id == parentMessage.ThreadId, ct);
                if (parentThread is not null)
                    return parentThread;
            }
        }

        // Check References header (last reference is usually the immediate parent)
        if (references is not null)
        {
            var refIds = references.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var refId in refIds.Reverse())
            {
                var refMsg = await _db.EmailMessages
                    .FirstOrDefaultAsync(m => m.AccountId == accountId
                        && m.MessageIdHeader == refId, ct);
                if (refMsg is not null)
                {
                    var refThread = await _db.EmailThreads
                        .FirstOrDefaultAsync(t => t.Id == refMsg.ThreadId, ct);
                    if (refThread is not null)
                        return refThread;
                }
            }
        }

        // Check for same-subject thread within 30 days
        var subjectThread = await _db.EmailThreads
            .FirstOrDefaultAsync(t => t.AccountId == accountId
                && t.Subject == subject
                && t.LastMessageAt >= DateTime.UtcNow.AddDays(-30), ct);

        if (subjectThread is not null)
            return subjectThread;

        // Create new thread
        var thread = new EmailThread
        {
            AccountId = accountId,
            Subject = subject,
            ProviderThreadId = gmailThreadId,
            ParticipantsJson = "[]",
            MessageCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.EmailThreads.Add(thread);
        return thread;
    }

    private static string? ExtractBodyPreview(GmailMessagePart payload)
    {
        try
        {
            // If it's a simple part with body data
            if (payload.Body?.Data is not null)
            {
                var text = DecodeBase64Url(payload.Body.Data);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    if (payload.MimeType == "text/html")
                        text = StripHtml(text);
                    return text.Length <= 500 ? text.Trim() : text[..500].Trim();
                }
            }

            // Walk MIME parts to find text/*
            if (payload.Parts is not null)
            {
                var plainPart = FindPartByMimeType(payload, "text/plain")
                    ?? FindPartByMimeType(payload, "text/html");

                if (plainPart?.Body?.Data is not null)
                {
                    var text = DecodeBase64Url(plainPart.Body.Data);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (plainPart.MimeType == "text/html")
                            text = StripHtml(text);
                        return text.Length <= 500 ? text.Trim() : text[..500].Trim();
                    }
                }
            }

            // Fallback: snippet from the message
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static GmailMessagePart? FindPartByMimeType(GmailMessagePart parent, string mimeType)
    {
        if (parent.Parts is null)
            return null;

        foreach (var part in parent.Parts)
        {
            if (part.MimeType?.StartsWith(mimeType, StringComparison.OrdinalIgnoreCase) == true
                && part.Body?.Data is not null)
                return part;

            if (part.Parts is not null)
            {
                var found = FindPartByMimeType(part, mimeType);
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    private static string DecodeBase64Url(string input)
    {
        // Convert base64url to standard base64
        var base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }
        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string StripHtml(string html)
    {
        var stripped = Regex.Replace(html, "<[^>]+>", " ");
        stripped = System.Net.WebUtility.HtmlDecode(stripped);
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }

    private static List<EmailAddressDto> ParseAddressHeader(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return [];

        var result = new List<EmailAddressDto>();
        // Simple parser for "Name <email>" or "email" comma-separated
        foreach (var part in headerValue.Split(','))
        {
            var trimmed = part.Trim();
            var match = Regex.Match(trimmed, @"^(.*?)\s*<(.+?)>\s*$");
            if (match.Success)
            {
                result.Add(new EmailAddressDto
                {
                    Name = match.Groups[1].Value.Trim().Trim('"'),
                    Email = match.Groups[2].Value.Trim()
                });
            }
            else if (!string.IsNullOrWhiteSpace(trimmed))
            {
                result.Add(new EmailAddressDto
                {
                    Name = null,
                    Email = trimmed
                });
            }
        }

        return result;
    }

    private static DateTime? ParseInternalDate(long? internalDate)
    {
        if (internalDate is null)
            return null;
        try
        {
            // Gmail internalDate is epoch milliseconds
            return DateTimeOffset.FromUnixTimeMilliseconds(internalDate.Value).UtcDateTime;
        }
        catch
        {
            return null;
        }
    }

    private static string SerializeGmailLabels(IList<string>? labels)
    {
        return labels is not null
            ? JsonSerializer.Serialize(labels)
            : "[]";
    }

    private static Dictionary<string, ulong> DeserializeSyncState(EmailAccount account)
    {
        if (string.IsNullOrEmpty(account.SyncStateJson))
            return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, ulong>>(account.SyncStateJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<GmailService> CreateGmailServiceAsync(EmailAccount account, CancellationToken ct)
    {
        var token = GetGmailToken(account);

        // Check if token is expired and needs refresh
        if (token.ExpiresAtUtc is not null && token.ExpiresAtUtc <= DateTime.UtcNow)
        {
            token = await RefreshAccessTokenAsync(token);
            // Persist updated token
            var json = JsonSerializer.Serialize(token);
            account.EncryptedCredentialBlob = _encryption.Protect(
                Encoding.UTF8.GetBytes(json), account.OwnerId);
            await _db.SaveChangesAsync(ct);
        }

        var credential = GoogleCredential.FromAccessToken(token.AccessToken);

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DotNetCloud"
        });
    }

    private static async Task<GmailToken> RefreshAccessTokenAsync(GmailToken token)
    {
        if (string.IsNullOrWhiteSpace(token.RefreshToken))
            throw new InvalidOperationException("Gmail token expired and no refresh token available.");

        using var http = new HttpClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = token.ClientId ?? "",
            ["client_secret"] = token.ClientSecret ?? "",
            ["refresh_token"] = token.RefreshToken,
            ["grant_type"] = "refresh_token"
        });

        var response = await http.PostAsync("https://oauth2.googleapis.com/token", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        return new GmailToken
        {
            AccessToken = root.GetProperty("access_token").GetString()!,
            RefreshToken = token.RefreshToken,
            ExpiresAtUtc = root.TryGetProperty("expires_in", out var expiresIn)
                ? DateTime.UtcNow.AddSeconds(expiresIn.GetInt32())
                : null,
            ClientId = token.ClientId,
            ClientSecret = token.ClientSecret
        };
    }

    private GmailToken GetGmailToken(EmailAccount account)
    {
        if (account.EncryptedCredentialBlob is null)
            throw new InvalidOperationException($"No credentials stored for account {account.Id}.");

        var bytes = _encryption.Unprotect(account.EncryptedCredentialBlob, account.OwnerId);
        var json = Encoding.UTF8.GetString(bytes);
        return JsonSerializer.Deserialize<GmailToken>(json)
            ?? throw new InvalidOperationException("Failed to deserialize Gmail token.");
    }

    private static EmailMailbox MapLabelToMailbox(Label label, Guid accountId)
    {
        var flags = MailboxFlags.None;
        var labelId = label.Id ?? "";

        if (labelId == "INBOX")
            flags |= MailboxFlags.Inbox;
        if (labelId == "SENT")
            flags |= MailboxFlags.Sent;
        if (labelId == "TRASH")
            flags |= MailboxFlags.Trash;
        if (labelId == "DRAFT")
            flags |= MailboxFlags.Drafts;
        if (labelId == "SPAM")
            flags |= MailboxFlags.Spam;

        return new EmailMailbox
        {
            AccountId = accountId,
            ProviderId = labelId,
            DisplayName = label.Name ?? labelId,
            SyncFlags = (int)flags
        };
    }

    private async Task ProcessGmailAttachmentsAsync(GmailService service, Message message, EmailMessage emailMessage, CancellationToken ct)
    {
        if (message.Payload?.Parts is null)
            return;

        foreach (var part in message.Payload.Parts)
        {
            if (string.IsNullOrWhiteSpace(part.Filename) || part.Body?.AttachmentId is null)
                continue;

            var fileName = part.Filename;
            var contentType = part.MimeType ?? "application/octet-stream";

            try
            {
                // Download attachment content via Gmail API
                var attachReq = service.Users.Messages.Attachments.Get("me", message.Id, part.Body.AttachmentId);
                var attachmentData = await attachReq.ExecuteAsync(ct);

                if (attachmentData?.Data is null)
                {
                    // Store metadata only
                    _db.EmailAttachments.Add(new EmailAttachment
                    {
                        Message = emailMessage,
                        FileName = fileName,
                        ContentType = contentType,
                        Size = part.Body.Size.GetValueOrDefault()
                    });
                    continue;
                }

                // Decode base64url data
                var bytes = Convert.FromBase64String(DecodeBase64Url(attachmentData.Data));
                await using var memStream = new MemoryStream(bytes);
                var result = await _attachmentStorage.StoreAsync(memStream, fileName, contentType, ct);

                var emailAttachment = new EmailAttachment
                {
                    Message = emailMessage,
                    FileName = fileName,
                    ContentType = contentType,
                    Size = result.Size,
                    StorageKey = result.StorageKey,
                    ContentHash = result.ContentHash,
                    ContentId = string.IsNullOrWhiteSpace(part.Body.AttachmentId) ? null : part.Body.AttachmentId,
                    CreatedAt = DateTime.UtcNow
                };

                _db.EmailAttachments.Add(emailAttachment);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download Gmail attachment {FileName} for message {MsgId}", fileName, message.Id);
                // Store metadata only as fallback
                _db.EmailAttachments.Add(new EmailAttachment
                {
                    Message = emailMessage,
                    FileName = fileName,
                    ContentType = contentType,
                    Size = part.Body.Size.GetValueOrDefault()
                });
            }
        }
    }

    private static string BuildMimeMessage(EmailAccount account, EmailSendRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"From: {account.DisplayName} <{account.EmailAddress}>");
        sb.AppendLine($"To: {string.Join(", ", request.To.Select(t => $"{t.Name} <{t.Email}>"))}");
        if (request.Cc is { Count: > 0 })
            sb.AppendLine($"Cc: {string.Join(", ", request.Cc.Select(c => $"{c.Name} <{c.Email}>"))}");
        if (request.InReplyToMessageId is not null)
            sb.AppendLine($"In-Reply-To: {request.InReplyToMessageId}");
        if (request.References is { Count: > 0 })
            sb.AppendLine($"References: {string.Join(" ", request.References)}");
        sb.AppendLine($"Subject: {request.Subject}");
        sb.AppendLine("MIME-Version: 1.0");
        sb.AppendLine("Content-Type: text/plain; charset=UTF-8");
        sb.AppendLine();
        sb.AppendLine(request.BodyPlainText ?? "");
        return sb.ToString();
    }
}

internal sealed record GmailToken
{
    public required string AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
}
