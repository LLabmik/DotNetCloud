using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Sync;

namespace DotNetCloud.Client.Core.VirtualFiles;

/// <summary>
/// Platform-specific virtual file system provider.
/// Implementations: <c>CloudFilterSyncProvider</c> (Windows), <c>FuseSyncFilesystem</c> (Linux).
/// </summary>
public interface IVirtualFileProvider : IAsyncDisposable
{
    /// <summary>
    /// Initializes the provider — registers the sync root with the OS (Windows)
    /// or mounts the FUSE filesystem (Linux).
    /// </summary>
    /// <param name="context">The sync context identifying the sync pairing.</param>
    /// <param name="ct">Cancellation token.</param>
    Task InitializeAsync(SyncContext context, CancellationToken ct = default);

    /// <summary>
    /// Creates metadata-only placeholder files from the server folder tree.
    /// Called during initial sync when <see cref="VirtualFileStorageMode.FilesOnDemand"/> is active.
    /// </summary>
    /// <param name="tree">The server folder tree response.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CreatePlaceholdersAsync(SyncTreeNodeResponse tree, CancellationToken ct = default);

    /// <summary>
    /// Downloads file content on demand and hydrates the placeholder.
    /// Called when a user or application opens a cloud-only file.
    /// </summary>
    /// <param name="localPath">Full local path to the file to hydrate.</param>
    /// <param name="nodeId">Server node ID of the file.</param>
    /// <param name="ct">Cancellation token.</param>
    Task HydrateFileAsync(string localPath, Guid nodeId, CancellationToken ct = default);

    /// <summary>
    /// Replaces hydrated file content with a placeholder, freeing local disk space.
    /// Pinned files are not dehydrated.
    /// </summary>
    /// <param name="localPath">Full local path to the file to dehydrate.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DehydrateFileAsync(string localPath, CancellationToken ct = default);

    /// <summary>
    /// Pins a file — marks it as "always keep on this device."
    /// Pinned files are exempt from dehydration and LRU eviction.
    /// </summary>
    /// <param name="localPath">Full local path to the file to pin.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PinFileAsync(string localPath, CancellationToken ct = default);

    /// <summary>
    /// Unpins a file — allows it to be dehydrated or evicted.
    /// </summary>
    /// <param name="localPath">Full local path to the file to unpin.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UnpinFileAsync(string localPath, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> if the file has local content (Hydrated or Pinned state).
    /// </summary>
    /// <param name="localPath">Full local path to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the file content is available locally.</returns>
    Task<bool> IsHydratedAsync(string localPath, CancellationToken ct = default);

    /// <summary>
    /// Shuts down the provider — unregisters the sync root (Windows)
    /// or unmounts the FUSE filesystem (Linux).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task ShutdownAsync(CancellationToken ct = default);
}
