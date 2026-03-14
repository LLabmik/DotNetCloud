using DotNetCloud.Modules.Files.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data.Services.Background;

/// <summary>
/// Background service that enforces file version retention policies.
/// Runs periodically to prune oldest unlabeled versions exceeding the configured limits.
/// </summary>
/// <remarks>
/// <para>Two policies are applied independently:</para>
/// <list type="bullet">
///   <item><description>
///     <b>Max count</b> — when a file has more versions than <see cref="VersionRetentionOptions.MaxVersionCount"/>,
///     the oldest unlabeled versions are deleted until the count is within the limit.
///   </description></item>
///   <item><description>
///     <b>Time-based</b> — unlabeled versions older than <see cref="VersionRetentionOptions.RetentionDays"/>
///     are deleted, provided at least one version always remains.
///   </description></item>
/// </list>
/// <para>Labeled versions are never auto-deleted by either policy.</para>
/// </remarks>
internal sealed class VersionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<VersionRetentionOptions> _options;
    private readonly ILogger<VersionCleanupService> _logger;

    public VersionCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<VersionRetentionOptions> options,
        ILogger<VersionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;
        using var timer = new PeriodicTimer(opts.CleanupInterval);

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
                _logger.LogError(ex, "Error during version cleanup");
            }
        }
    }

    /// <summary>
    /// Applies retention policies to all files. Exposed internally for testing.
    /// </summary>
    internal async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;

        if (opts.MaxVersionCount <= 0 && opts.RetentionDays <= 0)
            return; // Nothing configured — skip

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();

        // Get distinct file node IDs that have at least one version
        var fileNodeIds = await db.FileVersions
            .AsNoTracking()
            .Select(v => v.FileNodeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var totalDeleted = 0;

        foreach (var nodeId in fileNodeIds)
        {
            totalDeleted += await CleanupFileVersionsAsync(db, nodeId, opts, cancellationToken);
        }

        if (totalDeleted > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Version cleanup: pruned {Count} excess/expired versions across {Files} files",
                totalDeleted, fileNodeIds.Count);
        }
    }

    private static async Task<int> CleanupFileVersionsAsync(
        FilesDbContext db,
        Guid fileNodeId,
        VersionRetentionOptions opts,
        CancellationToken cancellationToken)
    {
        // Load all versions for this file, oldest first
        var versions = await db.FileVersions
            .Where(v => v.FileNodeId == fileNodeId)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync(cancellationToken);

        // Always keep at least one version
        if (versions.Count <= 1)
            return 0;

        var toDeleteIds = new HashSet<Guid>();

        // Policy 1: Max version count — delete oldest unlabeled versions
        if (opts.MaxVersionCount > 0 && versions.Count > opts.MaxVersionCount)
        {
            var excess = versions.Count - opts.MaxVersionCount;
            var candidates = versions.Where(v => v.Label is null).Take(excess);
            foreach (var v in candidates)
                toDeleteIds.Add(v.Id);
        }

        // Policy 2: Time-based retention — delete unlabeled versions older than RetentionDays
        if (opts.RetentionDays > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-opts.RetentionDays);
            foreach (var v in versions.Where(v => v.CreatedAt < cutoff && v.Label is null))
                toDeleteIds.Add(v.Id);
        }

        if (toDeleteIds.Count == 0)
            return 0;

        // Safety: ensure at least one version always remains by protecting the newest version
        var newestVersionId = versions[^1].Id;
        if (versions.Count - toDeleteIds.Count < 1)
            toDeleteIds.Remove(newestVersionId);

        var versionsToDelete = versions.Where(v => toDeleteIds.Contains(v.Id)).ToList();

        foreach (var version in versionsToDelete)
        {
            // Decrement chunk reference counts
            var versionChunks = await db.FileVersionChunks
                .Where(vc => vc.FileVersionId == version.Id)
                .ToListAsync(cancellationToken);

            foreach (var vc in versionChunks)
            {
                await ChunkReferenceHelper.DecrementAsync(db, vc.FileChunkId, cancellationToken);
            }

            db.FileVersionChunks.RemoveRange(versionChunks);
            db.FileVersions.Remove(version);
        }

        return versionsToDelete.Count;
    }
}
