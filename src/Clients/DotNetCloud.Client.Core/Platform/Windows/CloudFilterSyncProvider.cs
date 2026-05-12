// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Platform.Windows.CfApi;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.Core.Transfer;
using DotNetCloud.Client.Core.VirtualFiles;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Platform.Windows;

/// <summary>
/// Windows Cloud Filter API implementation of <see cref="IVirtualFileProvider"/>.
/// Registers a sync root with Windows, creates placeholders via CfApi, and
/// manages on-demand hydration via Cloud Filter callbacks.
/// This is the same API used by OneDrive and Microsoft OneDrive.
/// </summary>
public sealed class CloudFilterSyncProvider : IVirtualFileProvider
{
    private const uint CfRegisterFlagsNone = 0;
    private const uint CfDisconnectFlagsNone = 0;

    /// <summary>Provider ID GUID for DotNetCloud sync root registration.</summary>
    private static readonly Guid SdkProviderId = new("E25F5A8F-8B3A-4A3E-9A1C-7F5E3D2B1C4A");

    private readonly IChunkedTransferClient _chunkedTransfer;
    private readonly ILocalStateDb _localStateDb;
    private readonly VirtualFileSettings _settings;
    private readonly ILogger<CloudFilterSyncProvider> _logger;
    private readonly ILogger<CloudFilterCallbacks> _callbacksLogger;

    private CloudFilterCallbacks? _callbacks;
    private string? _syncRootPath;
    private string? _dbPath;
    private ulong _connectionKey;
    private bool _initialized;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="CloudFilterSyncProvider"/>.
    /// </summary>
    public CloudFilterSyncProvider(
        IChunkedTransferClient chunkedTransfer,
        ILocalStateDb localStateDb,
        VirtualFileSettings settings,
        ILogger<CloudFilterSyncProvider> logger,
        ILogger<CloudFilterCallbacks> callbacksLogger)
    {
        _chunkedTransfer = chunkedTransfer;
        _localStateDb = localStateDb;
        _settings = settings;
        _logger = logger;
        _callbacksLogger = callbacksLogger;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(SyncContext context, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _syncRootPath = context.LocalFolderPath;
        _dbPath = context.StateDatabasePath;

        _logger.LogInformation(
            "Initializing CloudFilterSyncProvider for {DisplayName} at {SyncRoot}",
            context.DisplayName, _syncRootPath);

        // Create the callback handler
        _callbacks = new CloudFilterCallbacks(
            _chunkedTransfer,
            _localStateDb,
            _settings,
            _callbacksLogger);

        _callbacks.SetContextPaths(_dbPath, _syncRootPath);

        try
        {
            // Register the sync root with Windows
            RegisterSyncRoot(context);

            // Connect to receive callbacks
            ConnectSyncRoot();

            _initialized = true;
            _logger.LogInformation(
                "CloudFilterSyncProvider initialized successfully for {DisplayName}",
                context.DisplayName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize CloudFilterSyncProvider for {DisplayName}", context.DisplayName);
            _callbacks?.Dispose();
            _callbacks = null;
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task CreatePlaceholdersAsync(SyncTreeNodeResponse tree, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssertInitialized();

        _logger.LogInformation("Creating placeholders from server tree root: {Name}", tree.Name);

        try
        {
            var placeholders = new List<CF_PLACEHOLDER_CREATE_INFO>();
            BuildPlaceholderList(tree, string.Empty, placeholders);

            if (placeholders.Count == 0)
            {
                _logger.LogInformation("No placeholders to create — tree is empty.");
                return;
            }

            // Create placeholders in batches to avoid excessive memory usage
            const int batchSize = 100;
            for (int i = 0; i < placeholders.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batch = placeholders.Skip(i).Take(batchSize).ToArray();
                var hResult = CfApiNative.CfCreatePlaceholders(
                    _syncRootPath!,
                    batch,
                    (uint)batch.Length,
                    CF_PLACEHOLDER_CREATE_FLAGS.MARK_IN_SYNC,
                    out var entriesProcessed);

                if (hResult < 0)
                {
                    _logger.LogWarning(
                        "CfCreatePlaceholders returned HRESULT={HResult} for batch {Batch}. Processed {Processed}/{Count}.",
                        hResult, i / batchSize, entriesProcessed, batch.Length);
                }

                // Check individual placeholder results for diagnostics
                for (int j = 0; j < batch.Length; j++)
                {
                    if (batch[j].CreateResult < 0 && batch[j].CreateResult != CfHResult.HR_CLOUD_FILE_ALREADY_CONNECTED)
                    {
                        var relPath = Marshal.PtrToStringUni(batch[j].RelativePath);
                        _logger.LogTrace(
                            "Placeholder creation result for {Path}: HRESULT={HResult}",
                            relPath, batch[j].CreateResult);
                    }
                }
            }

            _logger.LogInformation(
                "Created {Count} placeholders from server tree.", placeholders.Count);
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
            // Download the file content
            using var contentStream = await _chunkedTransfer.DownloadAsync(
                nodeId,
                cancellationToken: ct);

            if (contentStream == null)
            {
                _logger.LogWarning("Download returned null for {LocalPath}", localPath);
                return;
            }

            // Read into buffer for CfExecute TRANSFER_DATA
            using var ms = new MemoryStream();
            await contentStream.CopyToAsync(ms, ct);
            var data = ms.ToArray();

            // Pin the buffer so native code can read it
            var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                ExecuteTransferData(localPath, data, dataHandle);
            }
            finally
            {
                dataHandle.Free();
            }

            // Update hydration state in local DB
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
            // Call CfSetPinState(UNPINNED) to allow dehydration
            var hResult = CfApiNative.CfSetPinState(localPath, CF_PIN_STATE.UNPINNED, 0);
            if (hResult < 0)
            {
                _logger.LogWarning("CfSetPinState(UNPINNED) returned HRESULT={HResult} for {LocalPath}", hResult, localPath);
            }

            // Update hydration state in local DB
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
            var hResult = CfApiNative.CfSetPinState(localPath, CF_PIN_STATE.PINNED, 0);
            if (hResult < 0)
            {
                _logger.LogWarning("CfSetPinState(PINNED) returned HRESULT={HResult} for {LocalPath}", hResult, localPath);
            }

            // Update settings pin list
            _settings.PinList.Add(localPath);

            // Update local DB
            var record = await _localStateDb.GetFileRecordAsync(_dbPath!, localPath, ct);
            if (record != null)
            {
                record.HydrationState = HydrationState.Pinned;
                await _localStateDb.UpsertFileRecordAsync(_dbPath!, record, ct);
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
            var hResult = CfApiNative.CfSetPinState(localPath, CF_PIN_STATE.UNPINNED, 0);
            if (hResult < 0)
            {
                _logger.LogWarning("CfSetPinState(UNPINNED) returned HRESULT={HResult} for {LocalPath}", hResult, localPath);
            }

            // Update settings pin list
            _settings.PinList.Remove(localPath);

            // Update local DB
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
            // Query via CfGetPlaceholderInfo
            var infoSize = Marshal.SizeOf<CF_PLACEHOLDER_STANDARD_INFO>();
            var infoPtr = Marshal.AllocHGlobal(infoSize);
            try
            {
                var hResult = CfApiNative.CfGetPlaceholderInfo(
                    localPath,
                    0, // CfPlaceholderStandardInfo
                    infoPtr,
                    (uint)infoSize,
                    out _);

                if (hResult < 0)
                {
                    _logger.LogWarning("CfGetPlaceholderInfo returned HRESULT={HResult} for {LocalPath}", hResult, localPath);
                    return false;
                }

                var info = Marshal.PtrToStructure<CF_PLACEHOLDER_STANDARD_INFO>(infoPtr);
                return info.PinState == CF_PIN_STATE.PINNED ||
                       (info.PlaceholderState & 0x02) != 0; // CF_PLACEHOLDER_STATE_PARTIALLY_ON_DISK
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }
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

        _logger.LogInformation("Shutting down CloudFilterSyncProvider");

        if (_initialized)
        {
            try
            {
                int hResult;

                // Disconnect from callbacks
                if (_connectionKey != 0)
                {
                    hResult = CfApiNative.CfDisconnectSyncRoot(_connectionKey, CfDisconnectFlagsNone);
                    if (hResult < 0)
                    {
                        _logger.LogWarning("CfDisconnectSyncRoot returned HRESULT={HResult}", hResult);
                    }
                }

                // Unregister the sync root
                if (_syncRootPath != null)
                {
                    var reg = default(CF_SYNC_REGISTRATION);
                    hResult = CfApiNative.CfUnregisterSyncRoot(_syncRootPath, in reg, 0);
                    if (hResult < 0)
                    {
                        _logger.LogWarning("CfUnregisterSyncRoot returned HRESULT={HResult}", hResult);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during CloudFilterSyncProvider shutdown");
            }

            _initialized = false;
        }

        _callbacks?.Dispose();
        _callbacks = null;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        await ShutdownAsync();
        _disposed = true;
    }

    /// <summary>
    /// Registers the sync root with the Windows Cloud Filter platform.
    /// This enables shell integration: icon overlays, status column, right-click context menu.
    /// </summary>
    private void RegisterSyncRoot(SyncContext context)
    {
        var providerName = Marshal.StringToHGlobalUni("DotNetCloud");
        var providerVersion = Marshal.StringToHGlobalUni("0.1.0");

        try
        {
            var registration = new CF_SYNC_REGISTRATION
            {
                StructSize = (uint)Marshal.SizeOf<CF_SYNC_REGISTRATION>(),
                ProviderName = providerName,
                ProviderVersion = providerVersion,
                SyncRootIdentity = IntPtr.Zero,
                SyncRootIdentitySize = 0,
                FileIdentity = IntPtr.Zero,
                FileIdentitySize = 0,
                ProviderId = SdkProviderId,
            };

            var policies = new CF_SYNC_POLICIES
            {
                StructSize = (uint)Marshal.SizeOf<CF_SYNC_POLICIES>(),
                HydrationPolicy = CF_HYDRATION_POLICY.FULL,
                HydrationPolicyModifier = 0,
                PopulationPolicy = 0,
                PopulationPolicyModifier = 0,
                InSyncPolicy = 0,
                InSyncPolicyModifier = 0,
                HardLinkPolicy = 0,
            };

            var hResult = CfApiNative.CfRegisterSyncRoot(
                _syncRootPath!,
                in registration,
                in policies,
                CfRegisterFlagsNone);

            if (hResult < 0 && hResult != CfHResult.HR_CLOUD_FILE_ALREADY_CONNECTED)
            {
                _logger.LogWarning(
                    "CfRegisterSyncRoot returned HRESULT={HResult}. Sync root may already be registered.",
                    hResult);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(providerName);
            Marshal.FreeHGlobal(providerVersion);
        }
    }

    /// <summary>
    /// Connects to the sync root to receive Cloud Filter callbacks.
    /// </summary>
    private void ConnectSyncRoot()
    {
        var callbackTable = _callbacks!.GetCallbackRegistrationTable();

        var hResult = CfApiNative.CfConnectSyncRoot(
            _syncRootPath!,
            callbackTable,
            IntPtr.Zero,
            CF_CONNECT_FLAGS.REQUIRE_FULL_FILE_PATH | CF_CONNECT_FLAGS.REQUIRE_PROCESS_INFO,
            out _connectionKey);

        if (hResult < 0)
        {
            _logger.LogError("CfConnectSyncRoot failed with HRESULT={HResult}", hResult);
            throw new InvalidOperationException(
                $"Failed to connect sync root. HRESULT=0x{hResult:X8}");
        }

        _logger.LogDebug("Connected to sync root. ConnectionKey={Key}", _connectionKey);
    }

    /// <summary>
    /// Executes a CfExecute TRANSFER_DATA operation to write content to a placeholder.
    /// </summary>
    private void ExecuteTransferData(string localPath, byte[] data, GCHandle dataHandle)
    {
        var opParams = new CF_OPERATION_PARAMETERS
        {
            StructSize = (uint)Marshal.SizeOf<CF_OPERATION_PARAMETERS>(),
            OperationType = CF_OPERATION_TYPE.TRANSFER_DATA,
            Parameters = new CF_OPERATION_PARAM_UNION
            {
                TransferData = new CF_OPERATION_PARAM_TRANSFER_DATA
                {
                    CompletionStatus = CfHResult.S_OK,
                    Offset = 0,
                    Length = data.Length,
                    Buffer = dataHandle.AddrOfPinnedObject(),
                },
            },
        };

        var opParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<CF_OPERATION_PARAMETERS>());
        try
        {
            Marshal.StructureToPtr(opParams, opParamsPtr, false);
            var hResult = CfApiNative.CfExecute(
                opParamsPtr,
                (uint)Marshal.SizeOf<CF_OPERATION_PARAMETERS>());

            if (hResult < 0)
            {
                _logger.LogWarning("CfExecute TRANSFER_DATA returned HRESULT={HResult} for {LocalPath}",
                    hResult, localPath);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(opParamsPtr);
        }
    }

    /// <summary>
    /// Recursively builds a flat list of <see cref="CF_PLACEHOLDER_CREATE_INFO"/> from the server tree.
    /// </summary>
    private void BuildPlaceholderList(
        SyncTreeNodeResponse node,
        string parentRelativePath,
        List<CF_PLACEHOLDER_CREATE_INFO> placeholders)
    {
        var relativePath = string.IsNullOrEmpty(parentRelativePath)
            ? node.Name
            : Path.Combine(parentRelativePath, node.Name);

        var isFolder = string.Equals(node.NodeType, "Folder", StringComparison.OrdinalIgnoreCase);

        var fsMetadata = new CF_FS_METADATA
        {
            FileSize = isFolder ? 0 : node.Size,
            CreationTime = node.UpdatedAt.ToFileTime(),
            LastAccessTime = DateTime.UtcNow.ToFileTime(),
            LastWriteTime = node.UpdatedAt.ToFileTime(),
            ChangeTime = node.UpdatedAt.ToFileTime(),
            FileAttributes = isFolder ? (uint)System.IO.FileAttributes.Directory : (uint)System.IO.FileAttributes.Normal,
        };

        var relativePathPtr = Marshal.StringToHGlobalUni(relativePath);

        var placeholderInfo = new CF_PLACEHOLDER_CREATE_INFO
        {
            RelativePath = relativePathPtr,
            Flags = CF_PLACEHOLDER_CREATE_FLAGS.MARK_IN_SYNC,
            FileIdentity = IntPtr.Zero,
            FileIdentitySize = 0,
            FsMetadata = fsMetadata,
            CreateResult = 0,
        };

        placeholders.Add(placeholderInfo);

        // Recurse into children
        if (isFolder && node.Children != null)
        {
            foreach (var child in node.Children)
            {
                BuildPlaceholderList(child, relativePath, placeholders);
            }
        }
    }

    private void AssertInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "CloudFilterSyncProvider has not been initialized. Call InitializeAsync first.");
        }
    }
}
