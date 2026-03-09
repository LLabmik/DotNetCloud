using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
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

    // Tracks db paths that were recreated from scratch due to corruption during the last InitializeAsync call.
    private readonly ConcurrentDictionary<string, bool> _resetPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes a new <see cref="LocalStateDb"/>.</summary>
    public LocalStateDb(ILogger<LocalStateDb> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        var isCorrupt = false;

        try
        {
            await using var ctx = CreateContext(dbPath);
            await ctx.Database.EnsureCreatedAsync(cancellationToken);
            await RunSchemaEvolutionAsync(dbPath, cancellationToken);

            // Integrity check via raw ADO.NET to avoid interfering with EF connection state
            using var conn = new SqliteConnection(BuildConnectionString(dbPath));
            await conn.OpenAsync(cancellationToken);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var integrityResult = (string?)await cmd.ExecuteScalarAsync(cancellationToken);

            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                isCorrupt = true;
                _logger.LogError(
                    "SQLite integrity check returned '{Result}' for database at {DbPath}. Archiving and recreating.",
                    integrityResult, dbPath);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            isCorrupt = true;
            _logger.LogError(ex, "Failed to open or verify local state DB at {DbPath}. Treating as corrupt.", dbPath);
        }

        if (isCorrupt)
        {
            // Release all SQLite connection pool handles to allow file rename on Windows
            SqliteConnection.ClearAllPools();

            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            RenameIfExists(dbPath, $"{dbPath}.corrupt.{timestamp}");
            RenameIfExists($"{dbPath}-wal", $"{dbPath}-wal.corrupt.{timestamp}");
            RenameIfExists($"{dbPath}-shm", $"{dbPath}-shm.corrupt.{timestamp}");

            _resetPaths[dbPath] = true;

            await using var freshCtx = CreateContext(dbPath);
            await freshCtx.Database.EnsureCreatedAsync(cancellationToken);

            _logger.LogInformation(
                "Fresh local state DB created at {DbPath}. Corrupt files archived with .corrupt.{Timestamp} suffix. Full resync required.",
                dbPath, timestamp);
        }
        else
        {
            _resetPaths.TryRemove(dbPath, out _);
            _logger.LogDebug("Local state DB initialized at {DbPath}.", dbPath);
        }
    }

    /// <inheritdoc/>
    public bool WasRecentlyReset(string dbPath) => _resetPaths.ContainsKey(dbPath);

    /// <inheritdoc/>
    public async Task CheckpointWalAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        await ctx.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken);
        _logger.LogDebug("WAL checkpoint completed for {DbPath}.", dbPath);
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
        var now = DateTime.UtcNow;
        var rows = await ctx.PendingOperations
            .Where(r => r.NextRetryAt == null || r.NextRetryAt <= now)
            .OrderBy(r => r.QueuedAt)
            .ToListAsync(cancellationToken);
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

    /// <inheritdoc/>
    public async Task UpdateOperationRetryAsync(
        string dbPath,
        int operationId,
        int retryCount,
        DateTime? nextRetryAt,
        string? lastError,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var row = await ctx.PendingOperations.FindAsync([operationId], cancellationToken);
        if (row is not null)
        {
            row.RetryCount = retryCount;
            row.NextRetryAt = nextRetryAt;
            row.LastError = lastError;
            await ctx.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task MoveToFailedAsync(
        string dbPath,
        PendingOperationRecord operation,
        string lastError,
        CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);

        var pendingRow = await ctx.PendingOperations.FindAsync([operation.Id], cancellationToken);
        if (pendingRow is not null)
            ctx.PendingOperations.Remove(pendingRow);

        ctx.FailedOperations.Add(new FailedOperationDbRow
        {
            OperationType = operation.OperationType,
            LocalPath = operation switch
            {
                PendingUpload u => u.LocalPath,
                PendingDownload d => d.LocalPath,
                _ => null,
            },
            NodeId = operation switch
            {
                PendingUpload u => u.NodeId,
                PendingDownload d => d.NodeId,
                _ => null,
            },
            QueuedAt = operation.QueuedAt,
            RetryCount = operation.RetryCount,
            LastError = lastError,
            FailedAt = DateTime.UtcNow,
        });

        await ctx.SaveChangesAsync(cancellationToken);
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

    // ── Active Upload Sessions ───────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SaveActiveUploadSessionAsync(string dbPath, ActiveUploadSessionRecord record, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        ctx.ActiveUploadSessions.Add(record);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateActiveUploadSessionChunksAsync(string dbPath, Guid sessionId, IReadOnlyList<string> uploadedChunkHashes, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var row = await ctx.ActiveUploadSessions.FirstOrDefaultAsync(r => r.SessionId == sessionId, cancellationToken);
        if (row is not null)
        {
            row.UploadedChunkHashesJson = JsonSerializer.Serialize(uploadedChunkHashes);
            await ctx.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteActiveUploadSessionAsync(string dbPath, Guid sessionId, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var row = await ctx.ActiveUploadSessions.FirstOrDefaultAsync(r => r.SessionId == sessionId, cancellationToken);
        if (row is not null)
        {
            ctx.ActiveUploadSessions.Remove(row);
            await ctx.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActiveUploadSessionRecord>> GetActiveUploadSessionsAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        return await ctx.ActiveUploadSessions.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteStaleActiveUploadSessionsAsync(string dbPath, DateTime olderThan, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var stale = await ctx.ActiveUploadSessions
            .Where(r => r.CreatedAt < olderThan)
            .ToListAsync(cancellationToken);
        if (stale.Count > 0)
        {
            ctx.ActiveUploadSessions.RemoveRange(stale);
            await ctx.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Cleaned up {Count} stale upload session record(s) from {DbPath}.", stale.Count, dbPath);
        }
    }

    // ── Conflict Records ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SaveConflictRecordAsync(string dbPath, ConflictRecord record, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        ctx.ConflictRecords.Add(record);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConflictRecord>> GetUnresolvedConflictsAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        return await ctx.ConflictRecords
            .Where(r => r.ResolvedAt == null)
            .OrderByDescending(r => r.DetectedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConflictRecord>> GetConflictHistoryAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        await using var ctx = CreateContext(dbPath);
        return await ctx.ConflictRecords
            .Where(r => r.DetectedAt >= cutoff)
            .OrderByDescending(r => r.DetectedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ResolveConflictAsync(string dbPath, int conflictId, string resolution, CancellationToken cancellationToken = default)
    {
        await using var ctx = CreateContext(dbPath);
        var row = await ctx.ConflictRecords.FindAsync([conflictId], cancellationToken);
        if (row is not null)
        {
            row.Resolution = resolution;
            row.ResolvedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync(cancellationToken);
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private static string BuildConnectionString(string dbPath) =>
        $"Data Source={dbPath}";

    private static LocalStateDbContext CreateContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<LocalStateDbContext>()
            .UseSqlite(BuildConnectionString(dbPath))
            .Options;
        return new LocalStateDbContext(options);
    }

    /// <summary>
    /// Adds any missing columns/tables to an existing DB that was created before the current schema.
    /// EnsureCreatedAsync only creates tables for a brand-new DB; this handles upgrades.
    /// </summary>
    private static async Task RunSchemaEvolutionAsync(string dbPath, CancellationToken cancellationToken)
    {
        using var conn = new SqliteConnection(BuildConnectionString(dbPath));
        await conn.OpenAsync(cancellationToken);

        // Enable WAL mode — persisted in the DB file header, so only needs to run once
        await ExecuteNonQueryAsync(conn, "PRAGMA journal_mode=WAL;", cancellationToken);

        // Add new columns to PendingOperations if the DB predates them
        var pendingColumns = await GetColumnNamesAsync(conn, "PendingOperations", cancellationToken);
        if (!pendingColumns.Contains("NextRetryAt"))
            await ExecuteNonQueryAsync(conn, "ALTER TABLE PendingOperations ADD COLUMN NextRetryAt TEXT NULL", cancellationToken);
        if (!pendingColumns.Contains("LastError"))
            await ExecuteNonQueryAsync(conn, "ALTER TABLE PendingOperations ADD COLUMN LastError TEXT NULL", cancellationToken);

        // Create FailedOperations table if it doesn't exist (EnsureCreatedAsync on existing DBs doesn't add new tables)
        await ExecuteNonQueryAsync(conn, @"
            CREATE TABLE IF NOT EXISTS FailedOperations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OperationType TEXT NOT NULL,
                LocalPath TEXT NULL,
                NodeId TEXT NULL,
                QueuedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                RetryCount INTEGER NOT NULL DEFAULT 0,
                LastError TEXT NULL,
                FailedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'
            )", cancellationToken);

        // Create ActiveUploadSessions table for crash-resilient upload resumption
        await ExecuteNonQueryAsync(conn, @"
            CREATE TABLE IF NOT EXISTS ActiveUploadSessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL,
                LocalPath TEXT NOT NULL,
                NodeId TEXT NULL,
                TotalChunks INTEGER NOT NULL DEFAULT 0,
                UploadedChunkHashesJson TEXT NOT NULL DEFAULT '[]',
                FileSize INTEGER NOT NULL DEFAULT 0,
                FileModifiedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                CreatedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'
            )", cancellationToken);

        // Create ConflictRecords table for conflict tracking and auto-resolution history
        await ExecuteNonQueryAsync(conn, @"
            CREATE TABLE IF NOT EXISTS ConflictRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OriginalPath TEXT NOT NULL,
                ConflictCopyPath TEXT NOT NULL DEFAULT '',
                NodeId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                LocalModifiedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                RemoteModifiedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                DetectedAt TEXT NOT NULL DEFAULT '0001-01-01 00:00:00',
                ResolvedAt TEXT NULL,
                Resolution TEXT NULL,
                BaseContentHash TEXT NULL,
                AutoResolved INTEGER NOT NULL DEFAULT 0
            )", cancellationToken);
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(SqliteConnection conn, string tableName, CancellationToken cancellationToken)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
            columns.Add(reader.GetString(1)); // column index 1 is "name"
        return columns;
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection conn, string sql, CancellationToken cancellationToken)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void RenameIfExists(string source, string destination)
    {
        if (File.Exists(source))
            File.Move(source, destination, overwrite: true);
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
            NextRetryAt = u.NextRetryAt,
            LastError = u.LastError,
        },
        PendingDownload d => new PendingOperationDbRow
        {
            OperationType = "Download",
            LocalPath = d.LocalPath,
            NodeId = d.NodeId,
            QueuedAt = d.QueuedAt,
            RetryCount = d.RetryCount,
            NextRetryAt = d.NextRetryAt,
            LastError = d.LastError,
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
            NextRetryAt = row.NextRetryAt,
            LastError = row.LastError,
        },
        "Download" => new PendingDownload
        {
            Id = row.Id,
            NodeId = row.NodeId ?? Guid.Empty,
            LocalPath = row.LocalPath ?? string.Empty,
            QueuedAt = row.QueuedAt,
            RetryCount = row.RetryCount,
            NextRetryAt = row.NextRetryAt,
            LastError = row.LastError,
        },
        _ => throw new ArgumentException($"Unknown operation type: {row.OperationType}"),
    };
}
