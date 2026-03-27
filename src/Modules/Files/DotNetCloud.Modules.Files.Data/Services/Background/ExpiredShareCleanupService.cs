using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that periodically deletes expired shares from the database.
/// Runs every 6 hours and removes shares whose <c>ExpiresAt</c> is in the past.
/// </summary>
internal sealed class ExpiredShareCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredShareCleanupService> _logger;

    public ExpiredShareCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredShareCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during expired share cleanup");
            }
        }
    }

    internal async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();

        var now = DateTime.UtcNow;

        var expiredShares = await db.FileShares
            .Where(s => s.ExpiresAt != null && s.ExpiresAt.Value < now)
            .ToListAsync(cancellationToken);

        if (expiredShares.Count == 0)
            return;

        db.FileShares.RemoveRange(expiredShares);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} expired share(s)", expiredShares.Count);
    }
}
