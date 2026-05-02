using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Email.Events;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Service implementation for composing and sending emails.
/// Resolves the correct provider and creates sent-message records.
/// </summary>
public sealed class EmailSendService : IEmailSendService
{
    private readonly EmailDbContext _db;
    private readonly IEnumerable<IEmailProvider> _providers;
    private readonly IEventBus _eventBus;
    private readonly ILogger<EmailSendService> _logger;

    public EmailSendService(
        EmailDbContext db,
        IEnumerable<IEmailProvider> providers,
        IEventBus eventBus,
        ILogger<EmailSendService> logger)
    {
        _db = db;
        _providers = providers;
        _eventBus = eventBus;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(Guid accountId, EmailSendRequest request, CallerContext caller, CancellationToken ct = default)
    {
        var account = await _db.EmailAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == caller.UserId && !a.IsDeleted, ct)
            ?? throw new ValidationException(ErrorCodes.EmailAccountNotFound, "Email account not found.");

        if (!account.IsEnabled)
            throw new ValidationException(ErrorCodes.EmailAccountNotFound, "Email account is disabled.");

        var provider = _providers.FirstOrDefault(p => p.ProviderType == account.ProviderType)
            ?? throw new System.InvalidOperationException($"No email provider for type {account.ProviderType}.");

        await provider.SendAsync(account, request, ct);

        var bodyText = request.BodyPlainText ?? request.BodyHtml ?? "";

        var sentMessage = new EmailMessage
        {
            AccountId = account.Id,
            ProviderMessageId = $"sent-{Guid.NewGuid():N}",
            Subject = request.Subject,
            BodyPreview = bodyText.Length > 0 ? bodyText[..Math.Min(500, bodyText.Length)] : null,
            FromJson = System.Text.Json.JsonSerializer.Serialize(
                new[] { new EmailAddressDto { Name = account.DisplayName, Email = account.EmailAddress } }),
            ToJson = System.Text.Json.JsonSerializer.Serialize(request.To),
            CcJson = request.Cc is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.Cc) : "[]",
            BccJson = request.Bcc is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.Bcc) : "[]",
            DateReceived = DateTime.UtcNow,
            IsRead = true
        };

        _db.EmailMessages.Add(sentMessage);
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
