using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that auto-purges trash items older than the configured retention period
/// and garbage-collects unreferenced chunks.
/// </summary>
internal sealed class TrashCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TrashCleanupService> _logger;
    private readonly TrashRetentionOptions _options;

    public TrashCleanupService(IServiceScopeFactory scopeFactory, ILogger<TrashCleanupService> logger, IOptions<TrashRetentionOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.CleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            _logger.LogDebug("Trash cleanup cycle starting");
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
        var orgResolver = scope.ServiceProvider.GetService<IUserOrganizationResolver>();

        // Skip if retention is disabled
        if (_options.RetentionDays > 0 || _options.OrganizationOverrides.Count > 0)
        {
            var globalCutoff = _options.RetentionDays > 0
                ? DateTime.UtcNow - TimeSpan.FromDays(_options.RetentionDays)
                : (DateTime?)null;

            // Get all deleted items (we'll filter per-org retention in memory)
            var allTrash = await db.FileNodes
                .IgnoreQueryFilters()
                .Where(n => n.IsDeleted && n.DeletedAt.HasValue)
                .ToListAsync(cancellationToken);

            if (allTrash.Count == 0)
                goto ChunkGC;

            // Resolve per-org overrides if any are configured
            IReadOnlyDictionary<Guid, Guid>? userOrgs = null;
            if (_options.OrganizationOverrides.Count > 0 && orgResolver is not null)
            {
                var ownerIds = allTrash.Select(n => n.OwnerId).Distinct();
                userOrgs = await orgResolver.GetOrganizationIdsAsync(ownerIds, cancellationToken);
            }

            var expiredTrash = new List<FileNode>();
            foreach (var node in allTrash)
            {
                var retentionDays = _options.RetentionDays;

                // Check for per-org override
                if (userOrgs is not null &&
                    userOrgs.TryGetValue(node.OwnerId, out var orgId) &&
                    _options.OrganizationOverrides.TryGetValue(orgId, out var orgRetention))
                {
                    retentionDays = orgRetention;
                }

                if (retentionDays <= 0)
                    continue;

                var cutoff = DateTime.UtcNow - TimeSpan.FromDays(retentionDays);
                if (node.DeletedAt!.Value < cutoff)
                    expiredTrash.Add(node);
            }

            if (expiredTrash.Count > 0)
            {
                foreach (var node in expiredTrash)
                {
                    await PermanentDeleteNodeAsync(db, node, cancellationToken);
                }

                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Auto-purged {Count} trash items (global retention: {Days} days, {OrgOverrides} org overrides)",
                    expiredTrash.Count, _options.RetentionDays, _options.OrganizationOverrides.Count);
            }
        }

        ChunkGC:

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
                await ChunkReferenceHelper.DecrementAsync(db, vc.FileChunkId, cancellationToken);
            }
            db.FileVersionChunks.RemoveRange(versionChunks);
        }
        db.FileVersions.RemoveRange(versions);

        db.FileNodes.Remove(node);
    }
}
