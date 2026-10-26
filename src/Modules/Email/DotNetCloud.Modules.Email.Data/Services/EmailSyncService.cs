using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Performs email account syncs when the core asks for them.
/// </summary>
/// <remarks>
/// <para>
/// This used to be a <c>BackgroundService</c> that swept every enabled account on its own timer, so
/// the module decided by itself which user each sync belonged to. Syncs are now driven by the core:
/// it lists the due accounts here (<see cref="ListSyncCandidatesAsync"/>) and then invokes a sync
/// naming both the account and the user it has signed an assertion for
/// (<see cref="SyncAccountAsync"/>). The module never acts for a user nobody vouched for.
/// </para>
/// <para>
/// IMAP/SMTP and Gmail logic stays where it is — this service only orchestrates providers and
/// persistence, as before.
/// </para>
/// </remarks>
public sealed class EmailSyncService : IEmailSyncService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailSyncService> _logger;
    private readonly IBackgroundServiceTracker? _tracker;
    private readonly Dictionary<Guid, EmailSyncStatus> _statuses = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSyncService"/> class.
    /// </summary>
    public EmailSyncService(
        IServiceScopeFactory scopeFactory,
        ILogger<EmailSyncService> logger,
        IBackgroundServiceTracker? tracker = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _tracker = tracker;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EmailSyncCandidate>> ListSyncCandidatesAsync(
        TimeSpan minAge, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmailDbContext>();

        var cutoff = DateTime.UtcNow - (minAge > TimeSpan.Zero ? minAge : TimeSpan.Zero);

        // "Last sync" is the account's UpdatedAt, which a successful sync always refreshes.
        // Editing an account also bumps it, which only postpones that account's next sync.
        var accounts = await db.EmailAccounts
            .AsNoTracking()
            .Where(a => a.IsEnabled && !a.IsDeleted && a.UpdatedAt <= cutoff)
            .OrderBy(a => a.UpdatedAt)
            .Select(a => new { a.Id, a.OwnerId, a.UpdatedAt })
            .ToListAsync(ct);

        return accounts
            .Select(a => new EmailSyncCandidate(a.Id, a.OwnerId, a.UpdatedAt))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> SyncAccountAsync(Guid accountId, Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            _logger.LogWarning("Refusing to sync account {AccountId} for an empty user id", accountId);
            return false;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmailDbContext>();
        var providers = scope.ServiceProvider.GetRequiredService<IEnumerable<IEmailProvider>>();

        // Ownership is checked here, not by the caller: an account id must never be enough on its
        // own to make the module read somebody else's mailbox.
        var account = await db.EmailAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId && !a.IsDeleted, ct);

        if (account is null)
        {
            _logger.LogWarning(
                "Sync requested for account {AccountId} which does not belong to user {UserId}",
                accountId, userId);
            return false;
        }

        await SyncAccountInternalAsync(account, db, providers, ct);
        return true;
    }

    /// <inheritdoc />
    public Task<EmailSyncStatus?> GetSyncStatusAsync(Guid accountId, CancellationToken ct = default)
    {
        lock (_statuses)
        {
            return Task.FromResult(_statuses.GetValueOrDefault(accountId));
        }
    }

    private async Task SyncAccountInternalAsync(EmailAccount account, EmailDbContext db,
        IEnumerable<IEmailProvider> providers, CancellationToken ct)
    {
        var provider = providers.FirstOrDefault(p => p.ProviderType == account.ProviderType);
        if (provider is null)
        {
            _logger.LogWarning("No provider found for account {AccountId} with type {ProviderType}",
                account.Id, account.ProviderType);
            return;
        }

        _logger.LogInformation("Starting sync for account {AccountId} ({EmailAddress})",
            account.Id, account.EmailAddress);

        var status = new EmailSyncStatus
        {
            AccountId = account.Id,
            StartedAt = DateTime.UtcNow,
            Status = "Running"
        };

        lock (_statuses)
        {
            _statuses[account.Id] = status;
        }

        try
        {
            // Discover mailboxes
            var mailboxes = await provider.ListMailboxesAsync(account, ct);

            // Upsert mailboxes in DB
            foreach (var mb in mailboxes)
            {
                var existing = await db.EmailMailboxes
                    .FirstOrDefaultAsync(m => m.AccountId == account.Id && m.ProviderId == mb.ProviderId, ct);

                if (existing is null)
                {
                    db.EmailMailboxes.Add(mb);
                }
                else
                {
                    existing.DisplayName = mb.DisplayName;
                    existing.SyncFlags = mb.SyncFlags;
                }
            }

            await db.SaveChangesAsync(ct);

            // Reload mailboxes from DB to get correct IDs (the objects from
            // ListMailboxesAsync have locally-generated IDs that may differ)
            var dbMailboxes = await db.EmailMailboxes
                .AsNoTracking()
                .Where(m => m.AccountId == account.Id)
                .ToListAsync(ct);

            // Sync each mailbox
            var totalNew = 0;
            var totalUpdated = 0;
            var totalDeleted = 0;

            foreach (var mailbox in dbMailboxes)
            {
                var result = await provider.SyncMailboxAsync(account, mailbox, ct);
                totalNew += result.NewMessages;
                totalUpdated += result.UpdatedMessages;
                totalDeleted += result.DeletedMessages;

                if (result.SyncWatermark is not null)
                    account.SyncStateJson = result.SyncWatermark;
            }

            account.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            status.NewMessages = totalNew;
            status.UpdatedMessages = totalUpdated;
            status.DeletedMessages = totalDeleted;
            status.CompletedAt = DateTime.UtcNow;
            status.Status = "Completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for account {AccountId}", account.Id);
            status.CompletedAt = DateTime.UtcNow;
            status.Status = "Failed";
            status.ErrorMessage = ex.Message;
        }

        lock (_statuses)
        {
            _statuses[account.Id] = status;
        }
    }
}
