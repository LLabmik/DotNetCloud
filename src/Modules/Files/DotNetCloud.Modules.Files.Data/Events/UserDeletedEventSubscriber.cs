using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Models;
using DotNetCloud.Modules.Files.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Events;

/// <summary>
/// Handles <see cref="UserDeletedEvent"/> by cleaning up all Files module data
/// associated with the deleted user: quota, sync devices, sync counters,
/// upload sessions, file nodes, and physical file storage.
/// </summary>
/// <remarks>
/// This subscriber is critical for demo mode auto-cleanup but also serves
/// general user deletion scenarios. Chunk cleanup is content-address aware:
/// chunks still referenced by other users' files are preserved.
/// </remarks>
public sealed class UserDeletedEventSubscriber : IEventHandler<UserDeletedEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserDeletedEventSubscriber> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDeletedEventSubscriber"/> class.
    /// </summary>
    public UserDeletedEventSubscriber(
        IServiceScopeFactory scopeFactory,
        ILogger<UserDeletedEventSubscriber> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(UserDeletedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Cleaning up Files data for deleted user {UserId}",
            @event.UserId);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var storageEngine = scope.ServiceProvider.GetRequiredService<IFileStorageEngine>();

        var userId = @event.UserId;

        try
        {
            // ── Step 1: Collect storage paths before deleting FileNodes ──
            var storagePaths = await db.FileNodes
                .Where(f => f.OwnerId == userId && f.StoragePath != null)
                .Select(f => f.StoragePath!)
                .Distinct()
                .ToListAsync(cancellationToken);

            _logger.LogDebug(
                "Found {Count} distinct storage paths for user {UserId}",
                storagePaths.Count,
                userId);

            // ── Step 2: Delete FileNode records owned by the user ──
            var deletedFileCount = await db.FileNodes
                .Where(f => f.OwnerId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogInformation(
                "Deleted {Count} FileNode records for user {UserId}",
                deletedFileCount,
                userId);

            // ── Step 3: Delete FileQuota ──
            var deletedQuotaCount = await db.FileQuotas
                .Where(q => q.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogDebug(
                "Deleted {Count} FileQuota records for user {UserId}",
                deletedQuotaCount,
                userId);

            // ── Step 4: Delete SyncDevice records ──
            var deletedDeviceCount = await db.SyncDevices
                .Where(d => d.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogDebug(
                "Deleted {Count} SyncDevice records for user {UserId}",
                deletedDeviceCount,
                userId);

            // ── Step 5: Delete UserSyncCounter ──
            var deletedCounterCount = await db.UserSyncCounters
                .Where(c => c.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);

            _logger.LogDebug(
                "Deleted {Count} UserSyncCounter records for user {UserId}",
                deletedCounterCount,
                userId);

            // ── Step 6: Delete ChunkedUploadSession records owned by this user ──
            // Upload sessions are per-user; delete any where the userId matches
            // (sessions don't have an explicit OwnerId, so we clean up by matching
            // on the deleted user's FileNodes that were upload targets)
            // Since we already deleted FileNodes, clean up any orphan upload sessions.
            // For a thorough cleanup, delete sessions that reference now-deleted file nodes.
            var orphanSessions = await db.UploadSessions
                .Where(s => s.TargetFileNodeId.HasValue)
                .ToListAsync(cancellationToken);

            var deletedSessionCount = 0;
            foreach (var session in orphanSessions)
            {
                // Check if the target file node still exists
                var targetExists = await db.FileNodes
                    .AnyAsync(f => f.Id == session.TargetFileNodeId, cancellationToken);
                if (!targetExists)
                {
                    db.UploadSessions.Remove(session);
                    deletedSessionCount++;
                }
            }

            if (deletedSessionCount > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            _logger.LogDebug(
                "Deleted {Count} orphaned ChunkedUploadSession records for user {UserId}",
                deletedSessionCount,
                userId);

            // ── Step 7: Clean up physical files (content-address aware) ──
            foreach (var storagePath in storagePaths)
            {
                // Check if any remaining FileNode references this storage path
                var stillReferenced = await db.FileNodes
                    .AnyAsync(f => f.StoragePath == storagePath, cancellationToken);

                if (!stillReferenced)
                {
                    try
                    {
                        // Delete from disk; storage engine may throw if path is malformed
                        await storageEngine.DeleteAsync(storagePath, cancellationToken);
                        _logger.LogDebug(
                            "Deleted unreferenced chunk: {StoragePath}",
                            storagePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to delete chunk {StoragePath} for user {UserId}",
                            storagePath,
                            userId);
                    }
                }
                else
                {
                    _logger.LogDebug(
                        "Chunk {StoragePath} is still referenced by other files; preserving",
                        storagePath);
                }
            }

            _logger.LogInformation(
                "Completed Files cleanup for deleted user {UserId}: {FileCount} files, " +
                "{QuotaCount} quotas, {DeviceCount} devices, {PathCount} storage paths checked",
                userId,
                deletedFileCount,
                deletedQuotaCount,
                deletedDeviceCount,
                storagePaths.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error cleaning up Files data for deleted user {UserId}",
                userId);
            // Don't rethrow — handler isolation: failure in one subscriber
            // should not affect other subscribers.
        }
    }
}
