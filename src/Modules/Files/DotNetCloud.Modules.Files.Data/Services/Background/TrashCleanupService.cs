using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that auto-purges trash items older than 30 days
/// and garbage-collects unreferenced chunks. Runs every 6 hours.
/// </summary>
internal sealed class TrashCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan TrashRetention = TimeSpan.FromDays(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TrashCleanupService> _logger;

    public TrashCleanupService(IServiceScopeFactory scopeFactory, ILogger<TrashCleanupService> logger)
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
                _logger.LogError(ex, "Error during trash cleanup");
            }
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var storageEngine = scope.ServiceProvider.GetRequiredService<IFileStorageEngine>();

        var cutoff = DateTime.UtcNow - TrashRetention;

        // Find items deleted more than 30 days ago
        var expiredTrash = await db.FileNodes
            .IgnoreQueryFilters()
            .Where(n => n.IsDeleted && n.DeletedAt.HasValue && n.DeletedAt.Value < cutoff)
            .ToListAsync(cancellationToken);

        if (expiredTrash.Count > 0)
        {
            foreach (var node in expiredTrash)
            {
                await PermanentDeleteNodeAsync(db, node, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Auto-purged {Count} trash items older than {Days} days",
                expiredTrash.Count, TrashRetention.TotalDays);
        }

        // GC unreferenced chunks
        var orphanChunks = await db.FileChunks
            .Where(c => c.ReferenceCount <= 0)
            .ToListAsync(cancellationToken);

        foreach (var chunk in orphanChunks)
        {
            try
            {
                await storageEngine.DeleteAsync(chunk.StoragePath, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete orphan chunk storage at {Path}", chunk.StoragePath);
            }

            db.FileChunks.Remove(chunk);
        }

        if (orphanChunks.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Garbage-collected {Count} unreferenced chunks", orphanChunks.Count);
        }
    }

    private static async Task PermanentDeleteNodeAsync(FilesDbContext db, FileNode node, CancellationToken cancellationToken)
    {
        // Delete related data
        var shares = await db.FileShares.Where(s => s.FileNodeId == node.Id).ToListAsync(cancellationToken);
        db.FileShares.RemoveRange(shares);

        var tags = await db.FileTags.Where(t => t.FileNodeId == node.Id).ToListAsync(cancellationToken);
        db.FileTags.RemoveRange(tags);

        var comments = await db.FileComments.IgnoreQueryFilters().Where(c => c.FileNodeId == node.Id).ToListAsync(cancellationToken);
        db.FileComments.RemoveRange(comments);

        var versions = await db.FileVersions.Where(v => v.FileNodeId == node.Id).ToListAsync(cancellationToken);
        foreach (var version in versions)
        {
            var versionChunks = await db.FileVersionChunks.Where(vc => vc.FileVersionId == version.Id).ToListAsync(cancellationToken);
            foreach (var vc in versionChunks)
            {
                var chunk = await db.FileChunks.FindAsync([vc.FileChunkId], cancellationToken);
                if (chunk is not null)
                    chunk.ReferenceCount = Math.Max(0, chunk.ReferenceCount - 1);
            }
            db.FileVersionChunks.RemoveRange(versionChunks);
        }
        db.FileVersions.RemoveRange(versions);

        db.FileNodes.Remove(node);
    }
}
