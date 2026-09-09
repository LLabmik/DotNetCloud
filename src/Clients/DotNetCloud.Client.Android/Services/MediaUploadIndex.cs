using SQLite;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// SQLite-backed record of media items that have already been uploaded to the server.
/// Replaces the old "highest date_added watermark" + <c>Preferences</c> fingerprint keys,
/// which let a failed item slip past the watermark forever and grew without bound.
///
/// The index is the single source of truth for "is this gallery item already backed up?".
/// A row is only inserted <em>after</em> a successful upload, so anything that fails or
/// is interrupted simply stays out of the index and is retried on the next scan.
/// Stored in <see cref="FileSystem.AppDataDirectory"/> alongside the other app databases.
/// </summary>
internal sealed class MediaUploadIndex : IAsyncDisposable
{
    private SQLiteAsyncConnection? _db;

    private async Task<SQLiteAsyncConnection> GetDbAsync()
    {
        if (_db is not null)
            return _db;
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "media_upload.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        await _db.CreateTableAsync<UploadedMediaRow>().ConfigureAwait(false);
        return _db;
    }

    /// <summary>Returns <c>true</c> when an item with the given media key is already recorded as uploaded.</summary>
    public async Task<bool> ContainsAsync(string mediaKey, CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        return await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM UploadedMediaRow WHERE MediaKey = ?", mediaKey)
            .ConfigureAwait(false) > 0;
    }

    /// <summary>Returns the number of recorded uploads in the index.</summary>
    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        return await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM UploadedMediaRow")
            .ConfigureAwait(false);
    }

    /// <summary>Records a successfully uploaded media item (insert-or-replace by media key).</summary>
    public async Task RecordAsync(UploadedMediaRow row, CancellationToken ct = default)
    {
        var db = await GetDbAsync().ConfigureAwait(false);
        await db.InsertOrReplaceAsync(row).ConfigureAwait(false);
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
}

/// <summary>A media item that has been uploaded to the server, keyed by a stable local identity.</summary>
internal sealed class UploadedMediaRow
{
    /// <summary>
    /// Stable local key for the item, e.g. <c>"&lt;displayName&gt;|&lt;size&gt;|&lt;dateAddedSec&gt;"</c>
    /// for MediaStore items or the local file path for app-private spooled captures.
    /// </summary>
    [PrimaryKey]
    public string MediaKey { get; set; } = string.Empty;

    /// <summary>Original source, e.g. the MediaStore content URI or local pending file path.</summary>
    public string? SourceUri { get; set; }

    /// <summary>File name as uploaded to the server.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Size in bytes of the uploaded file.</summary>
    public long FileSize { get; set; }

    /// <summary>UTC ticks of the media date (used to pick the AutoUpload/YYYY/MM folder).</summary>
    public long DateAddedUtcTicks { get; set; }

    /// <summary>Server folder path the item was uploaded to (informational), e.g. <c>AutoUpload/2026/09</c>.</summary>
    public string? ServerFolder { get; set; }

    /// <summary>UTC ticks when the upload completed.</summary>
    public long UploadedAtUtcTicks { get; set; }
}
