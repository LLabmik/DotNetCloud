using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Data.Services;

/// <summary>
/// Background service that permanently deletes products that have been soft-deleted
/// for more than 30 days. Runs once on startup and then every 6 hours.
/// </summary>
public sealed class ProductCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProductCleanupBackgroundService> _logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    public ProductCleanupBackgroundService(IServiceProvider serviceProvider, ILogger<ProductCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once at startup
        await RunCleanupAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
                await RunCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var productService = scope.ServiceProvider.GetRequiredService<ProductService>();
            var db = scope.ServiceProvider.GetRequiredService<TracksDbContext>();

            var cutoff = DateTime.UtcNow - RetentionPeriod;

            // Find all products deleted more than 30 days ago
            var expiredProductIds = await db.Products
                .IgnoreQueryFilters()
                .Where(p => p.IsDeleted && p.DeletedAt != null && p.DeletedAt <= cutoff)
                .Select(p => p.Id)
                .ToListAsync(ct);

            if (expiredProductIds.Count == 0)
                return;

            _logger.LogInformation("Hard-deleting {Count} products deleted before {Cutoff:O}",
                expiredProductIds.Count, cutoff);

            foreach (var productId in expiredProductIds)
            {
                try
                {
                    await productService.HardDeleteProductAsync(productId, ct);
                    _logger.LogInformation("Permanently deleted product {ProductId}", productId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to hard-delete product {ProductId}", productId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product cleanup background service failed");
        }
    }
}
