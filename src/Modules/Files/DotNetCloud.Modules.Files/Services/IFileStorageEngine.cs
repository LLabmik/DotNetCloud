namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Abstracts physical file storage operations.
/// Implementations handle reading and writing file/chunk data to the underlying storage medium.
/// </summary>
public interface IFileStorageEngine
{
    /// <summary>
    /// Writes chunk data to storage.
    /// </summary>
    /// <param name="storagePath">Content-addressable storage path.</param>
    /// <param name="data">Chunk data to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteChunkAsync(string storagePath, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads chunk data from storage.
    /// </summary>
    /// <param name="storagePath">Content-addressable storage path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chunk data, or null if not found.</returns>
    Task<byte[]?> ReadChunkAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a read stream for a chunk.
    /// </summary>
    /// <param name="storagePath">Content-addressable storage path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read stream, or null if not found.</returns>
    Task<Stream?> OpenReadStreamAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a chunk exists in storage.
    /// </summary>
    /// <param name="storagePath">Content-addressable storage path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> ExistsAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a chunk from storage.
    /// </summary>
    /// <param name="storagePath">Content-addressable storage path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total size of all stored data in bytes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<long> GetTotalSizeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams <paramref name="source"/> to <paramref name="storagePath"/> without buffering the whole
    /// payload in memory. The write is atomic: the data is staged in a scratch file in the same
    /// directory and renamed into place only after it has been fully written and verified, so a
    /// crashed or failed write never leaves a truncated blob behind.
    /// </summary>
    /// <param name="storagePath">Destination storage path.</param>
    /// <param name="source">Source stream to copy from.</param>
    /// <param name="expectedLength">Expected byte length, verified after the write. Optional.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteFromStreamAsync(
        string storagePath,
        Stream source,
        long? expectedLength = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates the storage paths of every file (relative to the storage root, using forward
    /// slashes) underneath <paramref name="prefix"/>. Used by the orphan-blob reconciler and to find
    /// leftover <c>*.tmp-*</c> scratch files.
    /// </summary>
    /// <param name="prefix">Directory prefix to enumerate, e.g. <c>files</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<string> EnumerateStoragePathsAsync(
        string prefix,
        CancellationToken cancellationToken = default);
}
