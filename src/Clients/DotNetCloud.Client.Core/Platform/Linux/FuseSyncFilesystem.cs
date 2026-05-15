// Licensed under the Apache License, Version 2.0.

#if !WINDOWS_BUILD

#pragma warning disable CA1416 // Platform checks handled by conditional compilation

using System.Diagnostics;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.Core.Transfer;
using DotNetCloud.Client.Core.VirtualFiles;
using FuseDotNet;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Platform.Linux;

/// <summary>
/// Linux FUSE implementation of <see cref="IVirtualFileProvider"/>.
/// Mounts a FUSE filesystem at the sync folder path and presents the server's
/// file tree as a local directory. Content is downloaded on demand when files
/// are opened for reading.
/// </summary>
public sealed class FuseSyncFilesystem : IVirtualFileProvider
{
    private readonly IChunkedTransferClient _chunkedTransfer;
    private readonly ILocalStateDb _localStateDb;
    private readonly VirtualFileSettings _settings;
    private readonly LruCacheManager _cache;
    private readonly ILogger<FuseSyncFilesystem> _logger;
    private readonly ILogger<DotNetCloudFuseOperations> _operationsLogger;

    private DotNetCloudFuseOperations? _operations;
    private Thread? _mountThread;
    private CancellationTokenSource? _shutdownCts;
    private string? _syncRootPath;
    private string? _dbPath;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FuseSyncFilesystem"/>.
    /// </summary>
    public FuseSyncFilesystem(
        IChunkedTransferClient chunkedTransfer,
        ILocalStateDb localStateDb,
        VirtualFileSettings settings,
        LruCacheManager cache,
        ILogger<FuseSyncFilesystem> logger,
        ILogger<DotNetCloudFuseOperations> operationsLogger)
    {
        _chunkedTransfer = chunkedTransfer ?? throw new ArgumentNullException(nameof(chunkedTransfer));
        _localStateDb = localStateDb ?? throw new ArgumentNullException(nameof(localStateDb));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _operationsLogger = operationsLogger ?? throw new ArgumentNullException(nameof(operationsLogger));
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(SyncContext context, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _syncRootPath = context.LocalFolderPath;
        _dbPath = context.StateDatabasePath;

        _logger.LogInformation(
            "Initializing FuseSyncFilesystem for {DisplayName} at {SyncRoot}",
            context.DisplayName, _syncRootPath);

        // Verify FUSE is available
        CheckFuseAvailability();

        // Ensure the sync directory exists
        Directory.CreateDirectory(_syncRootPath);

        // Create the operations handler
        _operations = new DotNetCloudFuseOperations(
            _localStateDb,
            _chunkedTransfer,
            _settings,
            _cache,
            _operationsLogger);

        _operations.SetContextPaths(_syncRootPath, _dbPath);

        // Mount FUSE on a background thread (Fuse.Mount blocks until unmounted)
        _shutdownCts = new CancellationTokenSource();
        _mountThread = new Thread(() => MountFuse(context.DisplayName))
        {
            Name = $"FUSE-{context.DisplayName}",
            IsBackground = true,
        };
        _mountThread.Start();

        // Give the mount a moment to initialize
        await Task.Delay(500, ct);

        _initialized = true;
        _logger.LogInformation(
            "FuseSyncFilesystem initialized successfully for {DisplayName}",
            context.DisplayName);
    }

    /// <inheritdoc/>
    public async Task CreatePlaceholdersAsync(SyncTreeNodeResponse tree, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssertInitialized();

        _logger.LogInformation("Creating placeholders from server tree root: {Name}", tree.Name);

        try
        {
            var placeholders = new List<(string LocalPath, Guid NodeId, bool IsDirectory)>();
            BuildPlaceholderList(tree, _syncRootPath!, placeholders);

            if (placeholders.Count == 0)
            {
                _logger.LogInformation("No placeholders to create — tree is empty.");
                return;
            }

            foreach (var (localPath, nodeId, isDir) in placeholders)
            {
                ct.ThrowIfCancellationRequested();

                var record = new LocalFileRecord
                {
                    LocalPath = localPath,
                    NodeId = nodeId,
                    SyncStateTag = isDir ? "Directory" : "Synced",
                    HydrationState = HydrationState.CloudOnly,
                    LastSyncedAt = DateTime.UtcNow,
                    LocalModifiedAt = DateTime.UtcNow,
                };

                await _localStateDb.UpsertFileRecordAsync(_dbPath!, record, ct);
            }

            _logger.LogInformation(
                "Created {Count} placeholder metadata entries from server tree.",
                placeholders.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Placeholder creation was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create placeholders from server tree.");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task HydrateFileAsync(string localPath, Guid nodeId, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssertInitialized();

        _logger.LogDebug("Hydrating file: {LocalPath} (NodeId={NodeId})", localPath, nodeId);

        try
        {
            // Check cache first
            if (_cache[localPath] != null)
            {
                _logger.LogTrace("File already cached: {LocalPath}", localPath);
                return;
            }

            // Download content
            using var contentStream = await _chunkedTransfer.DownloadAsync(
                nodeId,
                cancellationToken: ct);

            if (contentStream == null)
            {
                _logger.LogWarning("Download returned null for {LocalPath}", localPath);
                return;
            }

            using var ms = new MemoryStream();
            await contentStream.CopyToAsync(ms, ct);
            var data = ms.ToArray();

            // Store in cache
            _cache[localPath] = data;

            // Update hydration state
            var record = await _localStateDb.GetFileRecordAsync(_dbPath!, localPath, ct);
            if (record != null)
            {
                record.HydrationState = HydrationState.Hydrated;
                await _localStateDb.UpsertFileRecordAsync(_dbPath!, record, ct);
            }

            _logger.LogInformation("Successfully hydrated {LocalPath} ({Length} bytes)", localPath, data.Length);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Hydration cancelled for {LocalPath}", localPath);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate file {LocalPath}", localPath);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DehydrateFileAsync(string localPath, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssertInitialized();

        _logger.LogDebug("Dehydrating file: {LocalPath}", localPath);

        try
        {
            // Check if pinned
            if (_settings.PinList.Contains(localPath))
            {
                _logger.LogWarning("Cannot dehydrate pinned file: {LocalPath}", localPath);
                return;
            }

            // Remove from cache
            _cache.Remove(localPath);

            // Update hydration state
            var record = await _localStateDb.GetFileRecordAsync(_dbPath!, localPath, ct);
            if (record != null)
            {
                record.HydrationState = HydrationState.CloudOnly;
                await _localStateDb.UpsertFileRecordAsync(_dbPath!, record, ct);
            }

            _logger.LogInformation("Successfully dehydrated {LocalPath}", localPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dehydrate file {LocalPath}", localPath);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task PinFileAsync(string localPath, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssertInitialized();

        _logger.LogDebug("Pinning file: {LocalPath}", localPath);

        try
        {
            _settings.PinList.Add(localPath);

            // Ensure hydrated
            if (_cache[localPath] == null)
            {
                var record = await _localStateDb.GetFileRecordAsync(_dbPath!, localPath, ct);
                if (record != null && record.HydrationState == HydrationState.CloudOnly)
                {
                    await HydrateFileAsync(localPath, record.NodeId, ct);
                }
            }

            // Update hydration state
            var fileRecord = await _localStateDb.GetFileRecordAsync(_dbPath!, localPath, ct);
            if (fileRecord != null)
            {
                fileRecord.HydrationState = HydrationState.Pinned;
                await _localStateDb.UpsertFileRecordAsync(_dbPath!, fileRecord, ct);
            }

            _logger.LogInformation("Pinned file {LocalPath}", localPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pin file {LocalPath}", localPath);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task UnpinFileAsync(string localPath, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssertInitialized();

        _logger.LogDebug("Unpinning file: {LocalPath}", localPath);

        try
        {
            _settings.PinList.Remove(localPath);

            // Update hydration state
            var record = await _localStateDb.GetFileRecordAsync(_dbPath!, localPath, ct);
            if (record != null)
            {
                record.HydrationState = HydrationState.Hydrated;
                await _localStateDb.UpsertFileRecordAsync(_dbPath!, record, ct);
            }

            _logger.LogInformation("Unpinned file {LocalPath}", localPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unpin file {LocalPath}", localPath);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsHydratedAsync(string localPath, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssertInitialized();

        try
        {
            // Check cache first
            if (_cache[localPath] != null)
                return true;

            var record = await _localStateDb.GetFileRecordAsync(_dbPath!, localPath, ct);
            if (record == null)
                return false;

            return record.HydrationState != HydrationState.CloudOnly;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check hydration state for {LocalPath}", localPath);
            return false;
        }
    }

    /// <inheritdoc/>
    public Task ShutdownAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation("Shutting down FuseSyncFilesystem");

        if (_initialized)
        {
            try
            {
                // Unmount FUSE
                UnmountFuse();

                _shutdownCts?.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during FUSE shutdown");
            }

            _initialized = false;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await ShutdownAsync();
        _shutdownCts?.Dispose();
        _disposed = true;
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private static void CheckFuseAvailability()
    {
        // Check for fusermount3
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "fusermount3",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };
            proc.Start();
            proc.WaitForExit(5000);

            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    "fusermount3 not found. Please install fuse3: sudo apt install fuse3");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to check for fusermount3. Please ensure fuse3 is installed: sudo apt install fuse3",
                ex);
        }

        // Check fuse group membership (warning only)
        try
        {
            var groups = Environment.GetEnvironmentVariable("GROUPS")
                ?? Environment.GetEnvironmentVariable("GROUP");
            // Simple check: try accessing /dev/fuse
            if (!File.Exists("/dev/fuse"))
            {
                throw new InvalidOperationException(
                    "/dev/fuse not found. FUSE kernel module may not be loaded. " +
                    "Try: sudo modprobe fuse");
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            // Non-critical check
        }
    }

    private void MountFuse(string displayName)
    {
        try
        {
            var mountArgs = new List<string>
            {
                _syncRootPath!,
                "-f", // Foreground mode (required for library-based mounting)
                "-o", $"fsname=dotnetcloud-{displayName}",
                "-o", "default_permissions",
                "-o", "allow_other", // Allow all users to access
            };

            var fuseLogger = new FuseLoggerAdapter(
                _operationsLogger);

            _logger.LogInformation(
                "Mounting FUSE filesystem at {SyncRoot} for {DisplayName}",
                _syncRootPath, displayName);

            Fuse.Mount(_operations!, mountArgs, fuseLogger);

            _logger.LogInformation(
                "FUSE filesystem unmounted for {DisplayName}",
                displayName);
        }
        catch (Exception ex)
        {
            if (_shutdownCts?.IsCancellationRequested == true)
            {
                _logger.LogInformation(
                    "FUSE mount was cancelled for {DisplayName}", displayName);
            }
            else
            {
                _logger.LogError(ex,
                    "FUSE mount failed for {DisplayName} at {SyncRoot}",
                    displayName, _syncRootPath);
            }
        }
    }

    private void UnmountFuse()
    {
        if (string.IsNullOrEmpty(_syncRootPath))
            return;

        try
        {
            _logger.LogInformation("Unmounting FUSE at {SyncRoot}", _syncRootPath);

            var fuseStartInfo = new ProcessStartInfo
            {
                FileName = "fusermount3",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            fuseStartInfo.ArgumentList.Add("-u");
            fuseStartInfo.ArgumentList.Add(_syncRootPath);

            using var proc = new Process { StartInfo = fuseStartInfo };
            proc.Start();
            proc.WaitForExit(10000);

            if (proc.ExitCode == 0)
            {
                _logger.LogInformation("FUSE unmounted successfully from {SyncRoot}", _syncRootPath);
            }
            else
            {
                var stderr = proc.StandardError.ReadToEnd();
                _logger.LogWarning(
                    "fusermount3 -u returned exit code {ExitCode}: {Stderr}",
                    proc.ExitCode, stderr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unmount FUSE at {SyncRoot}", _syncRootPath);
        }
    }

    private static void BuildPlaceholderList(
        SyncTreeNodeResponse node,
        string parentLocalPath,
        List<(string LocalPath, Guid NodeId, bool IsDirectory)> result)
    {
        var localPath = Path.Combine(parentLocalPath, node.Name);

        if (node.NodeType == "Folder" || node.Children?.Count > 0)
        {
            result.Add((localPath, node.NodeId, true));

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    BuildPlaceholderList(child, localPath, result);
                }
            }
        }
        else
        {
            result.Add((localPath, node.NodeId, false));
        }
    }

    private void AssertInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("FuseSyncFilesystem has not been initialized.");
    }
}

#endif
