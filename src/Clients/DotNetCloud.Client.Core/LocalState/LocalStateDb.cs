using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.LocalState;

/// <summary>
/// EF Core SQLite implementation of <see cref="ILocalStateDb"/>.
/// A separate DbContext instance is created per operation to support concurrent access.
/// </summary>
public sealed class LocalStateDb : ILocalStateDb
{
    private readonly ILogger<LocalStateDb> _logger;

    /// <summary>Initializes a new <see cref="LocalStateDb"/>.</summary>
    public LocalStateDb(ILogger<LocalStateDb> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        await ctx.Database.EnsureCreatedAsync(cancellationToken);
        _logger.LogDebug("Local state DB initialized at {DbPath}.", dbPath);
    }

    // ── File Records ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<LocalFileRecord?> GetFileRecordAsync(string dbPath, string localPath, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        return await ctx.FileRecords.FirstOrDefaultAsync(r => r.LocalPath == localPath, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LocalFileRecord?> GetFileRecordByNodeIdAsync(string dbPath, Guid nodeId, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        return await ctx.FileRecords.FirstOrDefaultAsync(r => r.NodeId == nodeId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpsertFileRecordAsync(string dbPath, LocalFileRecord record, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var existing = await ctx.FileRecords.FirstOrDefaultAsync(r => r.LocalPath == record.LocalPath, cancellationToken);
        if (existing is null)
        {
            ctx.FileRecords.Add(record);
        }
        else
        {
            existing.NodeId = record.NodeId;
            existing.ContentHash = record.ContentHash;
            existing.LastSyncedAt = record.LastSyncedAt;
            existing.LocalModifiedAt = record.LocalModifiedAt;
            existing.SyncStateTag = record.SyncStateTag;
        }
        await ctx.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveFileRecordAsync(string dbPath, string localPath, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var existing = await ctx.FileRecords.FirstOrDefaultAsync(r => r.LocalPath == localPath, cancellationToken);
        if (existing is not null)
        {
            ctx.FileRecords.Remove(existing);
            await ctx.SaveChangesAsync(cancellationToken);
        }
    }

    // ── Pending Operations ──────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task QueueOperationAsync(string dbPath, PendingOperationRecord operation, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var row = MapToRow(operation);
        ctx.PendingOperations.Add(row);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PendingOperationRecord>> GetPendingOperationsAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var rows = await ctx.PendingOperations.OrderBy(r => r.QueuedAt).ToListAsync(cancellationToken);
        return rows.Select(MapFromRow).ToList();
    }

    /// <inheritdoc/>
    public async Task<PendingOperationCount> GetPendingOperationCountAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var uploads = await ctx.PendingOperations.CountAsync(r => r.OperationType == "Upload", cancellationToken);
        var downloads = await ctx.PendingOperations.CountAsync(r => r.OperationType == "Download", cancellationToken);
        return new PendingOperationCount { Uploads = uploads, Downloads = downloads };
    }

    /// <inheritdoc/>
    public async Task RemoveOperationAsync(string dbPath, int operationId, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var row = await ctx.PendingOperations.FindAsync([operationId], cancellationToken);
        if (row is not null)
        {
            ctx.PendingOperations.Remove(row);
            await ctx.SaveChangesAsync(cancellationToken);
        }
    }

    // ── Sync Checkpoint ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<DateTime?> GetCheckpointAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var row = await ctx.Checkpoints.FindAsync([1], cancellationToken);
        return row?.LastSyncedAt;
    }

    /// <inheritdoc/>
    public async Task UpdateCheckpointAsync(string dbPath, DateTime checkpoint, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var row = await ctx.Checkpoints.FindAsync([1], cancellationToken);
        if (row is null)
        {
            ctx.Checkpoints.Add(new SyncCheckpointRow { Id = 1, LastSyncedAt = checkpoint });
        }
        else
        {
            row.LastSyncedAt = checkpoint;
        }
        await ctx.SaveChangesAsync(cancellationToken);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private static LocalStateDbContext CreateContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<LocalStateDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new LocalStateDbContext(options);
    }

    private static PendingOperationDbRow MapToRow(PendingOperationRecord op) => op switch
    {
        PendingUpload u => new PendingOperationDbRow
        {
            OperationType = "Upload",
            LocalPath = u.LocalPath,
            NodeId = u.NodeId,
            QueuedAt = u.QueuedAt,
            RetryCount = u.RetryCount,
        },
        PendingDownload d => new PendingOperationDbRow
        {
            OperationType = "Download",
            LocalPath = d.LocalPath,
            NodeId = d.NodeId,
            QueuedAt = d.QueuedAt,
            RetryCount = d.RetryCount,
        },
        _ => throw new ArgumentException($"Unknown operation type: {op.GetType().Name}"),
    };

    private static PendingOperationRecord MapFromRow(PendingOperationDbRow row) => row.OperationType switch
    {
        "Upload" => new PendingUpload
        {
            Id = row.Id,
            LocalPath = row.LocalPath ?? string.Empty,
            NodeId = row.NodeId,
            QueuedAt = row.QueuedAt,
            RetryCount = row.RetryCount,
        },
        "Download" => new PendingDownload
        {
            Id = row.Id,
            NodeId = row.NodeId ?? Guid.Empty,
            LocalPath = row.LocalPath ?? string.Empty,
            QueuedAt = row.QueuedAt,
            RetryCount = row.RetryCount,
        },
        _ => throw new ArgumentException($"Unknown operation type: {row.OperationType}"),
    };
}
