using SQLite;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// SQLite-backed <see cref="IPendingMessageQueue"/> implementation.
/// The queue is stored in a dedicated table in the app's data directory.
/// </summary>
internal sealed class SqlitePendingMessageQueue : IPendingMessageQueue, IAsyncDisposable
{
    private SQLiteAsyncConnection? _db;

    private async Task<SQLiteAsyncConnection> GetDbAsync()
    {
        if (_db is not null) return _db;
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "pending_messages.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        await _db.CreateTableAsync<PendingMessageRow>().ConfigureAwait(false);
        return _db;
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(Guid channelId, string content, CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        await db.InsertAsync(new PendingMessageRow
        {
            ChannelId = channelId.ToString(),
            Content = content,
            EnqueuedAtTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PendingMessage>> GetAllAsync(CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        var rows = await db.QueryAsync<PendingMessageRow>(
            "SELECT * FROM PendingMessageRow ORDER BY EnqueuedAtTicks ASC").ConfigureAwait(false);

        return rows
            .Select(r => new PendingMessage(
                r.Id,
                Guid.Parse(r.ChannelId),
                r.Content,
                DateTimeOffset.FromUnixTimeMilliseconds(r.EnqueuedAtTicks)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(IEnumerable<long> rowIds, CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        foreach (var id in rowIds)
            await db.ExecuteAsync("DELETE FROM PendingMessageRow WHERE Id = ?", id).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        return await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM PendingMessageRow").ConfigureAwait(false);
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

    [Table("PendingMessageRow")]
    private sealed class PendingMessageRow
    {
        [PrimaryKey, AutoIncrement]
        public long Id { get; set; }

        public string ChannelId { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public long EnqueuedAtTicks { get; set; }
    }
}
