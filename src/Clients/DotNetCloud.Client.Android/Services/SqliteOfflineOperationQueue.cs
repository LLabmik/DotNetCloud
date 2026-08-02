using SQLite;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// SQLite-backed <see cref="IOfflineOperationQueue"/> implementation.
/// The queue is stored in a dedicated table in the app's data directory and survives
/// app restarts, so operations are never lost while the device is offline.
/// </summary>
internal sealed class SqliteOfflineOperationQueue : IOfflineOperationQueue, IAsyncDisposable
{
    private SQLiteAsyncConnection? _db;

    private async Task<SQLiteAsyncConnection> GetDbAsync()
    {
        if (_db is not null)
            return _db;
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "offline_queue.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        await _db.CreateTableAsync<OfflineOperationRow>().ConfigureAwait(false);
        return _db;
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(OfflineOperationType operationType, string payloadJson, CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        await db.InsertAsync(new OfflineOperationRow
        {
            OperationType = (int)operationType,
            PayloadJson = payloadJson,
            EnqueuedAtTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QueuedOperation>> GetAllAsync(CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        var rows = await db.QueryAsync<OfflineOperationRow>(
            "SELECT * FROM OfflineOperationRow ORDER BY EnqueuedAtTicks ASC, Id ASC").ConfigureAwait(false);

        return rows
            .Select(r => new QueuedOperation(
                r.Id,
                (OfflineOperationType)r.OperationType,
                r.PayloadJson,
                DateTimeOffset.FromUnixTimeMilliseconds(r.EnqueuedAtTicks)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(IEnumerable<long> rowIds, CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        foreach (var id in rowIds)
            await db.ExecuteAsync("DELETE FROM OfflineOperationRow WHERE Id = ?", id).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        return await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM OfflineOperationRow").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.CloseAsync().ConfigureAwait(false);
            _db = null;
        }
    }

    [Table("OfflineOperationRow")]
    private sealed class OfflineOperationRow
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }

        public int OperationType { get; set; }

        public string PayloadJson { get; set; } = string.Empty;

        public long EnqueuedAtTicks { get; set; }
    }
}
