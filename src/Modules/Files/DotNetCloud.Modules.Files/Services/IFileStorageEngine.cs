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
}
