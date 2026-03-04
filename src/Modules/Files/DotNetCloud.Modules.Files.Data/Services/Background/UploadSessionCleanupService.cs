using DotNetCloud.Modules.Files.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that periodically cleans up expired upload sessions and orphaned chunks.
/// Runs every hour.
/// </summary>
internal sealed class UploadSessionCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UploadSessionCleanupService> _logger;

    public UploadSessionCleanupService(IServiceScopeFactory scopeFactory, ILogger<UploadSessionCleanupService> logger)
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
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during upload session cleanup");
            }
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();

        var now = DateTime.UtcNow;

        // Expire stale sessions
        var expiredSessions = await db.UploadSessions
            .Where(s => s.Status == UploadSessionStatus.InProgress && s.ExpiresAt < now)
            .ToListAsync(cancellationToken);

        foreach (var session in expiredSessions)
        {
            session.Status = UploadSessionStatus.Expired;
            session.UpdatedAt = now;
        }

        if (expiredSessions.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Expired {Count} stale upload sessions", expiredSessions.Count);
        }
    }
}
