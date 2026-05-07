using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Email.Events;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AttachmentStorageResult = DotNetCloud.Modules.Email.Services.AttachmentStorageResult;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Service implementation for composing and sending emails.
/// Resolves the correct provider and creates sent-message records.
/// </summary>
public sealed class EmailSendService : IEmailSendService
{
    private readonly EmailDbContext _db;
    private readonly CoreDbContext _coreDb;
    private readonly IEnumerable<IEmailProvider> _providers;
    private readonly IEventBus _eventBus;
    private readonly IAttachmentStorage _attachmentStorage;
    private readonly ILogger<EmailSendService> _logger;

    public EmailSendService(
        EmailDbContext db,
        CoreDbContext coreDb,
        IEnumerable<IEmailProvider> providers,
        IEventBus eventBus,
        IAttachmentStorage attachmentStorage,
        ILogger<EmailSendService> logger)
    {
        _db = db;
        _coreDb = coreDb;
        _providers = providers;
        _eventBus = eventBus;
        _attachmentStorage = attachmentStorage;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(Guid accountId, EmailSendRequest request, CallerContext caller, CancellationToken ct = default)
    {
        // Block email sending for demo users (skip for system callers)
        if (caller.Type == CallerType.User && caller.UserId != Guid.Empty)
        {
            var isDemoUser = await _coreDb.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == caller.UserId && u.IsDemoUser, ct);

            if (isDemoUser)
            {
                throw new ValidationException(
                    "EMAIL_SENDING_DISABLED_DEMO",
                    "Email sending is not available in demo mode. Upgrade to a full account to send emails.");
            }
        }

        var account = await _db.EmailAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == caller.UserId && !a.IsDeleted, ct)
            ?? throw new ValidationException(ErrorCodes.EmailAccountNotFound, "Email account not found.");

        if (!account.IsEnabled)
            throw new ValidationException(ErrorCodes.EmailAccountNotFound, "Email account is disabled.");

        var provider = _providers.FirstOrDefault(p => p.ProviderType == account.ProviderType)
            ?? throw new System.InvalidOperationException($"No email provider for type {account.ProviderType}.");

        await provider.SendAsync(account, request, ct);

        var bodyText = request.BodyPlainText ?? request.BodyHtml ?? "";
        var now = DateTime.UtcNow;

        // Find existing thread (for replies) or create a new one for this sent message.
        EmailThread thread;
        if (request.InReplyToMessageId is not null)
        {
            var parent = await _db.EmailMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.AccountId == account.Id && m.MessageIdHeader == request.InReplyToMessageId, ct);
            if (parent is not null)
                thread = await _db.EmailThreads.FirstAsync(t => t.Id == parent.ThreadId, ct);
            else
                thread = null!;
        }
        else
        {
            thread = null!;
        }

        if (thread is null)
        {
            thread = new EmailThread
            {
                AccountId = account.Id,
                Subject = request.Subject,
                ProviderThreadId = $"sent-{Guid.NewGuid():N}",
                ParticipantsJson = "[]",
                MessageCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.EmailThreads.Add(thread);
        }

        var sentMessage = new EmailMessage
        {
            AccountId = account.Id,
            ThreadId = thread.Id,
            ProviderMessageId = $"sent-{Guid.NewGuid():N}",
            Subject = request.Subject,
            BodyPreview = bodyText.Length > 0 ? bodyText[..Math.Min(500, bodyText.Length)] : null,
            FromJson = System.Text.Json.JsonSerializer.Serialize(
                new[] { new EmailAddressDto { Name = account.DisplayName, Email = account.EmailAddress } }),
            ToJson = System.Text.Json.JsonSerializer.Serialize(request.To),
            CcJson = request.Cc is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.Cc) : "[]",
            BccJson = request.Bcc is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.Bcc) : "[]",
            DateReceived = now,
            IsRead = true
        };

        thread.MessageCount++;
        thread.LastMessageAt = now;
        thread.UpdatedAt = now;

        // Associate the sent message with the account's Sent Items mailbox
        var sentMailbox = await _db.EmailMailboxes
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.AccountId == account.Id
                && (m.SyncFlags & (int)MailboxFlags.Sent) == (int)MailboxFlags.Sent, ct);
        if (sentMailbox is not null)
            sentMessage.MailboxId = sentMailbox.Id;

        _db.EmailMessages.Add(sentMessage);

        // Merge sent message participants into thread ParticipantsJson
        var currentParticipants = System.Text.Json.JsonSerializer.Deserialize<List<EmailAddressDto>>(thread.ParticipantsJson) ?? [];
        var fromAddresses = System.Text.Json.JsonSerializer.Deserialize<List<EmailAddressDto>>(sentMessage.FromJson) ?? [];
        var toAddresses = System.Text.Json.JsonSerializer.Deserialize<List<EmailAddressDto>>(sentMessage.ToJson) ?? [];
        List<EmailAddressDto> ccAddresses = [];
        if (!string.IsNullOrWhiteSpace(sentMessage.CcJson) && sentMessage.CcJson != "[]")
            ccAddresses = System.Text.Json.JsonSerializer.Deserialize<List<EmailAddressDto>>(sentMessage.CcJson) ?? [];
        var mergedParticipants = currentParticipants
            .Concat(fromAddresses).Concat(toAddresses).Concat(ccAddresses)
            .DistinctBy(a => a.Email.ToLowerInvariant())
            .ToList();
        thread.ParticipantsJson = System.Text.Json.JsonSerializer.Serialize(mergedParticipants);

        // Persist attachment records for sent message
        if (request.Attachments is { Count: > 0 })
        {
            foreach (var attRef in request.Attachments)
            {
                var size = attRef.Size;
                if (size <= 0)
                {
                    // Try to get size from storage if not provided in request
                    size = await _attachmentStorage.GetSizeAsync(attRef.StorageKey, ct);
                }

                _db.EmailAttachments.Add(new EmailAttachment
                {
                    Message = sentMessage,
                    FileName = attRef.FileName,
                    ContentType = attRef.ContentType,
                    Size = size,
                    StorageKey = attRef.StorageKey,
                    ContentHash = null, // Not hashed during upload; stores as StorageKey
                    ContentId = attRef.ContentId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Email sent from account {AccountId} to {Recipients}, messageId={MessageId}",
            accountId, string.Join(", ", request.To.Select(t => t.Email)), sentMessage.Id);

        await _eventBus.PublishAsync(new EmailSentEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            AccountId = accountId,
            OwnerId = caller.UserId,
            Subject = request.Subject,
            To = request.To.Select(t => t.Email).ToList()
        }, caller, ct);
    }
}
