using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Service implementation for managing email accounts.
/// </summary>
public sealed class EmailAccountService : IEmailAccountService
{
    private readonly EmailDbContext _db;
    private readonly EmailCredentialEncryptionService _encryption;
    private readonly ILogger<EmailAccountService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailAccountService"/> class.
    /// </summary>
    public EmailAccountService(EmailDbContext db, EmailCredentialEncryptionService encryption, ILogger<EmailAccountService> logger)
    {
        _db = db;
        _encryption = encryption;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailAccount>> ListAsync(CallerContext caller, CancellationToken ct = default)
    {
        return await _db.EmailAccounts.AsNoTracking()
            .Where(a => a.OwnerId == caller.UserId)
            .OrderBy(a => a.DisplayName)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailMailbox>> ListMailboxesAsync(Guid accountId, CallerContext caller, CancellationToken ct = default)
    {
        // Verify ownership
        var owns = await _db.EmailAccounts
            .AnyAsync(a => a.Id == accountId && a.OwnerId == caller.UserId, ct);

        if (!owns)
            throw new ValidationException(ErrorCodes.EmailAccountNotFound, "Email account not found.");

        return await _db.EmailMailboxes.AsNoTracking()
            .Where(m => m.AccountId == accountId)
            .OrderBy(m => m.DisplayName)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<EmailAccount?> GetAsync(Guid id, CallerContext caller, CancellationToken ct = default)
    {
        return await _db.EmailAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == caller.UserId, ct);
    }

    /// <inheritdoc />
    public async Task<EmailAccount> CreateAsync(CreateEmailAccountRequest request, CallerContext caller, CancellationToken ct = default)
    {
        var account = new EmailAccount
        {
            OwnerId = caller.UserId,
            ProviderType = request.ProviderType,
            DisplayName = request.DisplayName,
            EmailAddress = request.EmailAddress,
            IsEnabled = true
        };

        if (request.CredentialsJson is not null)
        {
            var credentialBytes = System.Text.Encoding.UTF8.GetBytes(request.CredentialsJson);
            account.EncryptedCredentialBlob = _encryption.Protect(credentialBytes, caller.UserId);
        }

        _db.EmailAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Email account created: {AccountId} ({EmailAddress})", account.Id, account.EmailAddress);
        return account;
    }

    /// <inheritdoc />
    public async Task<EmailAccount> UpdateAsync(Guid id, UpdateEmailAccountRequest request, CallerContext caller, CancellationToken ct = default)
    {
        var account = await _db.EmailAccounts
            .FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == caller.UserId, ct)
            ?? throw new ValidationException(ErrorCodes.EmailAccountNotFound, "Email account not found.");

        if (request.DisplayName is not null) account.DisplayName = request.DisplayName;
        if (request.IsEnabled.HasValue) account.IsEnabled = request.IsEnabled.Value;
        account.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Email account updated: {AccountId}", id);
        return account;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CallerContext caller, CancellationToken ct = default)
    {
        var account = await _db.EmailAccounts
            .FirstOrDefaultAsync(a => a.Id == id && a.OwnerId == caller.UserId, ct)
            ?? throw new ValidationException(ErrorCodes.EmailAccountNotFound, "Email account not found.");

        account.IsDeleted = true;
        account.DeletedAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Email account deleted: {AccountId}", id);
    }
}
