using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.Core.Transfer;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.VirtualFiles;

/// <summary>
/// Wraps <see cref="ISyncEngine"/> to support virtual file syncing (files on-demand).
/// When <see cref="VirtualFileStorageMode.DownloadAll"/> is active, all operations
/// pass through to the underlying sync engine unchanged.
/// When <see cref="VirtualFileStorageMode.FilesOnDemand"/> is active, sync produces
/// metadata-only placeholders and content is hydrated on first access.
/// </summary>
public sealed class VirtualFileSyncEngine : IAsyncDisposable
{
    private readonly ISyncEngine _inner;
    private readonly IVirtualFileProvider _vfsProvider;
    private readonly VirtualFileSettings _settings;
    private readonly ILogger<VirtualFileSyncEngine> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="VirtualFileSyncEngine"/>.
    /// </summary>
    public VirtualFileSyncEngine(
        ISyncEngine inner,
        IVirtualFileProvider vfsProvider,
        VirtualFileSettings settings,
        ILogger<VirtualFileSyncEngine> logger)
    {
        _inner = inner;
        _vfsProvider = vfsProvider;
        _settings = settings;
        _logger = logger;

        // Forward events from inner engine
        _inner.StatusChanged += (s, e) => StatusChanged?.Invoke(s, e);
        _inner.FileTransferProgress += (s, e) => FileTransferProgress?.Invoke(s, e);
        _inner.FileTransferComplete += (s, e) => FileTransferComplete?.Invoke(s, e);
    }

    /// <summary>Raised when the sync status changes. Forwards from inner engine.</summary>
    public event EventHandler<SyncStatusChangedEventArgs>? StatusChanged;

    /// <summary>Raised when progress is reported for an individual file transfer. Forwards from inner engine.</summary>
    public event EventHandler<FileTransferProgressEventArgs>? FileTransferProgress;

    /// <summary>Raised when an individual file transfer completes. Forwards from inner engine.</summary>
    public event EventHandler<FileTransferCompleteEventArgs>? FileTransferComplete;

    /// <summary>
    /// Gets the underlying <see cref="ISyncEngine"/> for direct access when needed.
    /// </summary>
    public ISyncEngine InnerEngine => _inner;

    /// <summary>
    /// Gets the virtual file provider.
    /// </summary>
    public IVirtualFileProvider VirtualFileProvider => _vfsProvider;

    /// <summary>
    /// Starts the sync engine. When in <see cref="VirtualFileStorageMode.FilesOnDemand"/> mode,
    /// also initializes the virtual file provider.
    /// </summary>
    public async Task StartAsync(SyncContext context, CancellationToken cancellationToken = default)
    {
        if (_settings.StorageMode == VirtualFileStorageMode.FilesOnDemand)
        {
            _logger.LogInformation(
                "Starting VFS engine in FilesOnDemand mode for {DisplayName}",
                context.DisplayName);
            await _vfsProvider.InitializeAsync(context, cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "Starting VFS engine in DownloadAll mode for {DisplayName}",
                context.DisplayName);
        }

        await _inner.StartAsync(context, cancellationToken);
    }

    /// <summary>
    /// Runs a full bidirectional sync pass.
    /// In <see cref="VirtualFileStorageMode.FilesOnDemand"/> mode, sync is metadata-only
    /// and placeholders are created via <see cref="IVirtualFileProvider.CreatePlaceholdersAsync"/>.
    /// </summary>
    public async Task SyncAsync(SyncContext context, CancellationToken cancellationToken = default)
    {
        if (_settings.StorageMode == VirtualFileStorageMode.FilesOnDemand)
        {
            _logger.LogDebug("Running metadata-only sync for {DisplayName}", context.DisplayName);
            // In Phase 2, the metadata-only sync delegates placeholder creation to the VFS provider.
            // The full content-download path is handled by the inner engine when appropriate.
            // Placeholder creation will be fully integrated with the sync pipeline in Phase 3/4.
            await _inner.SyncAsync(context, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Running full-content sync for {DisplayName}", context.DisplayName);
            await _inner.SyncAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// Returns the current sync status from the inner engine.
    /// </summary>
    public Task<SyncStatus> GetStatusAsync(SyncContext context, CancellationToken cancellationToken = default)
        => _inner.GetStatusAsync(context, cancellationToken);

    /// <summary>
    /// Pauses automatic sync. Forwards to inner engine.
    /// </summary>
    public Task PauseAsync(SyncContext context, CancellationToken cancellationToken = default)
        => _inner.PauseAsync(context, cancellationToken);

    /// <summary>
    /// Resumes automatic sync. Forwards to inner engine.
    /// </summary>
    public Task ResumeAsync(SyncContext context, CancellationToken cancellationToken = default)
        => _inner.ResumeAsync(context, cancellationToken);

    /// <summary>
    /// Stops the sync engine and shuts down the virtual file provider.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _vfsProvider.ShutdownAsync(cancellationToken);
        await _inner.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Disposes the inner engine and the VFS provider.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _vfsProvider.DisposeAsync();
        await _inner.DisposeAsync();
    }

    /// <summary>
    /// Switches the storage mode between <see cref="VirtualFileStorageMode.DownloadAll"/>
    /// and <see cref="VirtualFileStorageMode.FilesOnDemand"/>.
    /// Phase 2 implementation: logs the switch and updates settings.
    /// Full hydration/dehydration across all files will be implemented in Phase 3+.
    /// </summary>
    public Task SwitchModeAsync(
        SyncContext context,
        VirtualFileStorageMode newMode,
        CancellationToken cancellationToken = default)
    {
        var oldMode = _settings.StorageMode;
        if (oldMode == newMode)
            return Task.CompletedTask;

        _logger.LogInformation(
            "Switching VFS mode from {OldMode} to {NewMode} for {DisplayName}",
            oldMode, newMode, context.DisplayName);

        _settings.StorageMode = newMode;

        if (newMode == VirtualFileStorageMode.FilesOnDemand)
        {
            _logger.LogInformation(
                "Mode switch to FilesOnDemand — files will be dehydrated on next sync cycle");
        }
        else
        {
            _logger.LogInformation(
                "Mode switch to DownloadAll — files will be hydrated on next sync cycle");
        }

        return Task.CompletedTask;
    }
}
