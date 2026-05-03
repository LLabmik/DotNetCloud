using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Email.Data.Services;

/// <summary>
/// Background service that periodically syncs enabled email accounts.
/// Uses PeriodicTimer to poll every 5 minutes.
/// </summary>
public sealed class EmailSyncBackgroundService : BackgroundService, IEmailSyncService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailSyncBackgroundService> _logger;
    private readonly Dictionary<Guid, EmailSyncStatus> _statuses = new();
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);

    public EmailSyncBackgroundService(IServiceScopeFactory scopeFactory, ILogger<EmailSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SyncAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmailDbContext>();
        var providers = scope.ServiceProvider.GetRequiredService<IEnumerable<IEmailProvider>>();

        var account = await db.EmailAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, ct);

        if (account is null)
        {
            _logger.LogWarning("Sync requested for non-existent account {AccountId}", accountId);
            return;
        }

        await SyncAccountInternalAsync(account, db, providers, ct);
    }

    /// <inheritdoc />
    public async Task SyncAllAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmailDbContext>();
        var providers = scope.ServiceProvider.GetRequiredService<IEnumerable<IEmailProvider>>();

        var accounts = await db.EmailAccounts
            .Where(a => a.IsEnabled && !a.IsDeleted)
            .ToListAsync(ct);

        foreach (var account in accounts)
        {
            try
            {
                await SyncAccountInternalAsync(account, db, providers, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync account {AccountId}", account.Id);
            }
        }
    }

    /// <inheritdoc />
    public Task<EmailSyncStatus?> GetSyncStatusAsync(Guid accountId, CancellationToken ct = default)
    {
        lock (_statuses)
        {
            return Task.FromResult(_statuses.GetValueOrDefault(accountId));
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email sync background service starting");

        using var timer = new PeriodicTimer(_syncInterval);

        // Initial sync on startup
        try
        {
            await SyncAllAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial email sync");
        }

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SyncAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic email sync");
            }
        }

        _logger.LogInformation("Email sync background service stopping");
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
