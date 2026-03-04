using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that recalculates storage quotas for all users daily.
/// </summary>
internal sealed class QuotaRecalculationService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuotaRecalculationService> _logger;

    public QuotaRecalculationService(IServiceScopeFactory scopeFactory, ILogger<QuotaRecalculationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RecalculateAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during quota recalculation");
            }
        }
    }

    private async Task RecalculateAllAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();

        var quotas = await db.FileQuotas.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var count = 0;

        foreach (var quota in quotas)
        {
            var usedBytes = await db.FileNodes
                .AsNoTracking()
                .Where(n => n.OwnerId == quota.UserId && n.NodeType == FileNodeType.File)
                .SumAsync(n => n.Size, cancellationToken);

            quota.UsedBytes = usedBytes;
            quota.LastCalculatedAt = now;
            quota.UpdatedAt = now;
            count++;
        }

        if (count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Recalculated quotas for {Count} users", count);
        }
    }
}
