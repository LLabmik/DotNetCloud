using SQLite;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// SQLite-backed local message cache using sqlite-net-pcl.
/// Database file is stored in <see cref="FileSystem.AppDataDirectory"/>.
/// </summary>
internal sealed class SqliteMessageCache : ILocalMessageCache, IAsyncDisposable
{
    private SQLiteAsyncConnection? _db;

    private async Task<SQLiteAsyncConnection> GetDbAsync()
    {
        if (_db is not null) return _db;
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "message_cache.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        await _db.CreateTableAsync<MessageRow>().ConfigureAwait(false);
        return _db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CachedMessage>> GetRecentAsync(Guid channelId, int count = 50, CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        var channelStr = channelId.ToString();
        var rows = await db.QueryAsync<MessageRow>(
            "SELECT * FROM MessageRow WHERE ChannelId = ? ORDER BY SentAtTicks DESC LIMIT ?",
            channelStr, count).ConfigureAwait(false);

        return rows
            .OrderBy(r => r.SentAtTicks)
            .Select(r => new CachedMessage(
                Guid.Parse(r.Id),
                Guid.Parse(r.ChannelId),
                r.SenderName,
                r.Content,
                DateTimeOffset.FromUnixTimeMilliseconds(r.SentAtTicks)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task UpsertAsync(IEnumerable<CachedMessage> messages, CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        var rows = messages.Select(m => new MessageRow
        {
            Id = m.Id.ToString(),
            ChannelId = m.ChannelId.ToString(),
            SenderName = m.SenderName,
            Content = m.Content,
            SentAtTicks = m.SentAt.ToUnixTimeMilliseconds()
        });
        await db.InsertOrReplaceAllAsync(rows).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PruneAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        var cutoffTicks = DateTimeOffset.UtcNow.Subtract(maxAge).ToUnixTimeMilliseconds();
        await db.ExecuteAsync("DELETE FROM MessageRow WHERE SentAtTicks < ?", cutoffTicks).ConfigureAwait(false);
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

    [Table("MessageRow")]
    private sealed class MessageRow
    {
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;
        public string ChannelId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public long SentAtTicks { get; set; }
    }
}
