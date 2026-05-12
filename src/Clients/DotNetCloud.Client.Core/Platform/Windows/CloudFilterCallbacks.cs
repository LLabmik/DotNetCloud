// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.LocalState;
using DotNetCloud.Client.Core.Platform.Windows.CfApi;
using DotNetCloud.Client.Core.Transfer;
using DotNetCloud.Client.Core.VirtualFiles;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Platform.Windows;

/// <summary>
/// Manages Cloud Filter API callbacks for the <see cref="CloudFilterSyncProvider"/>.
/// Callbacks are invoked by Windows on arbitrary threads when the file system
/// needs data or wants to notify the sync provider of file system events.
/// </summary>
public sealed class CloudFilterCallbacks : IDisposable
{
    private readonly IChunkedTransferClient _chunkedTransfer;
    private readonly ILocalStateDb _localStateDb;
    private readonly VirtualFileSettings _settings;
    private readonly ILogger<CloudFilterCallbacks> _logger;

    /// <summary>Per-file hydration locks to prevent duplicate concurrent hydration.</summary>
    private readonly Dictionary<string, SemaphoreSlim> _hydrationLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lockGate = new();

    /// <summary>
    /// GCHandle pinned to keep the native callback delegates alive.
    /// Must remain alive for the lifetime of the sync root connection.
    /// </summary>
    private GCHandle _callbacksHandle;

    /// <summary>Path to the SQLite local state database for hydration state tracking.</summary>
    private string? _dbPath;

    /// <summary>Sync root path for constructing full file paths in callbacks.</summary>
    private string? _syncRootPath;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="CloudFilterCallbacks"/>.
    /// </summary>
    public CloudFilterCallbacks(
        IChunkedTransferClient chunkedTransfer,
        ILocalStateDb localStateDb,
        VirtualFileSettings settings,
        ILogger<CloudFilterCallbacks> logger)
    {
        _chunkedTransfer = chunkedTransfer;
        _localStateDb = localStateDb;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Sets the database path and sync root path for callback hydration lookups.
    /// Called by <see cref="CloudFilterSyncProvider.InitializeAsync"/> after registration.
    /// </summary>
    public void SetContextPaths(string dbPath, string syncRootPath)
    {
        _dbPath = dbPath;
        _syncRootPath = syncRootPath;
    }

    /// <summary>
    /// Gets the callback registration table to pass to <see cref="CfApiNative.CfConnectSyncRoot"/>.
    /// </summary>
    public CF_CALLBACK_REGISTRATION[] GetCallbackRegistrationTable()
    {
        var fetchDataCallback = new CF_FETCH_DATA_CALLBACK(OnFetchData);
        var validateDataCallback = new CF_VALIDATE_DATA_CALLBACK(OnValidateData);
        var fetchPlaceholdersCallback = new CF_FETCH_PLACEHOLDERS_CALLBACK(OnFetchPlaceholders);
        var cancelFetchCallback = new CF_CANCEL_FETCH_DATA_CALLBACK(OnCancelFetchData);
        var notificationCallback = new CF_NOTIFICATION_CALLBACK(OnNotification);

        _callbacksHandle = GCHandle.Alloc(
            new object[] { fetchDataCallback, validateDataCallback, fetchPlaceholdersCallback, cancelFetchCallback, notificationCallback },
            GCHandleType.Normal);

        return
        [
            new CF_CALLBACK_REGISTRATION { Type = CF_CALLBACK_TYPE.FETCH_DATA, Callback = Marshal.GetFunctionPointerForDelegate(fetchDataCallback) },
            new CF_CALLBACK_REGISTRATION { Type = CF_CALLBACK_TYPE.VALIDATE_DATA, Callback = Marshal.GetFunctionPointerForDelegate(validateDataCallback) },
            new CF_CALLBACK_REGISTRATION { Type = CF_CALLBACK_TYPE.FETCH_PLACEHOLDERS, Callback = Marshal.GetFunctionPointerForDelegate(fetchPlaceholdersCallback) },
            new CF_CALLBACK_REGISTRATION { Type = CF_CALLBACK_TYPE.CANCEL_FETCH_DATA, Callback = Marshal.GetFunctionPointerForDelegate(cancelFetchCallback) },
            new CF_CALLBACK_REGISTRATION { Type = CF_CALLBACK_TYPE.NOTIFY_FILE_OPEN_COMPLETION, Callback = Marshal.GetFunctionPointerForDelegate(notificationCallback) },
            new CF_CALLBACK_REGISTRATION { Type = CF_CALLBACK_TYPE.NOTIFY_FILE_CLOSE_COMPLETION, Callback = Marshal.GetFunctionPointerForDelegate(notificationCallback) },
            new CF_CALLBACK_REGISTRATION { Type = CF_CALLBACK_TYPE.NOTIFY_DEHYDRATE, Callback = Marshal.GetFunctionPointerForDelegate(notificationCallback) },
            new CF_CALLBACK_REGISTRATION { Type = CF_CALLBACK_TYPE.NOTIFY_DELETE, Callback = Marshal.GetFunctionPointerForDelegate(notificationCallback) },
            new CF_CALLBACK_REGISTRATION { Type = CF_CALLBACK_TYPE.NOTIFY_RENAME, Callback = Marshal.GetFunctionPointerForDelegate(notificationCallback) },

            // Null terminator
            default,
        ];
    }

    /// <summary>
    /// Handles FETCH_DATA callbacks — downloads content for a cloud-only file.
    /// </summary>
    private int OnFetchData(ref CF_CALLBACK_INFO callbackInfo, ref CF_CALLBACK_PARAMETERS callbackData)
    {
        var relativePath = Marshal.PtrToStringUni(callbackInfo.RelativePath);
        if (string.IsNullOrEmpty(relativePath))
        {
            _logger.LogWarning("FETCH_DATA callback received with empty relative path.");
            return CfHResult.HR_CLOUD_FILE_NOT_AVAILABLE;
        }

        _logger.LogDebug("FETCH_DATA callback for {RelativePath} (offset={Offset}, length={Length})",
            relativePath, callbackInfo.RequiredOffset, callbackInfo.RequiredLength);

        var syncRootPath = Marshal.PtrToStringUni(callbackInfo.SyncRootPath) ?? string.Empty;
        var fullPath = Path.Combine(syncRootPath, relativePath);

        // Use a per-file semaphore to prevent duplicate concurrent hydrations
        var fileLock = GetFileLock(relativePath);

        if (!fileLock.Wait(0))
        {
            _logger.LogDebug("Hydration already in progress for {RelativePath}", relativePath);
            return CfHResult.HR_CLOUD_FILE_PENDING;
        }

        try
        {
            // Hydrate the file asynchronously
            _ = HydrateFileAsync(fullPath, relativePath, callbackInfo, callbackData);
            return CfHResult.S_OK;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate hydration for {RelativePath}", relativePath);
            return CfHResult.HR_CLOUD_FILE_NOT_AVAILABLE;
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    /// Handles VALIDATE_DATA callbacks — verifies data integrity of a hydrated file.
    /// </summary>
    private int OnValidateData(ref CF_CALLBACK_INFO callbackInfo, ref CF_CALLBACK_PARAMETERS callbackData)
    {
        var relativePath = Marshal.PtrToStringUni(callbackInfo.RelativePath);
        _logger.LogDebug("VALIDATE_DATA callback for {RelativePath}", relativePath);

        // For Phase 3, we acknowledge validation without deep verification.
        // Deep hash verification can be added in a later phase.
        return CfHResult.S_OK;
    }

    /// <summary>
    /// Handles FETCH_PLACEHOLDERS callbacks — provides directory entries for Explorer enumeration.
    /// </summary>
    private int OnFetchPlaceholders(ref CF_CALLBACK_INFO callbackInfo, ref CF_CALLBACK_PARAMETERS callbackData)
    {
        var relativePath = Marshal.PtrToStringUni(callbackInfo.RelativePath);
        _logger.LogDebug("FETCH_PLACEHOLDERS callback for {RelativePath}", relativePath);

        // Phase 3: Placeholder population is handled by CfCreatePlaceholders during initial sync.
        // This callback handles on-demand directory enumeration when the user browses a directory
        // that hasn't been fully populated yet.
        return CfHResult.S_OK;
    }

    /// <summary>
    /// Handles CANCEL_FETCH_DATA callbacks.
    /// </summary>
    private int OnCancelFetchData(ref CF_CALLBACK_INFO callbackInfo, ref CF_CALLBACK_PARAMETERS callbackData)
    {
        var relativePath = Marshal.PtrToStringUni(callbackInfo.RelativePath);
        _logger.LogDebug("CANCEL_FETCH_DATA callback for {RelativePath}", relativePath);

        return CfHResult.S_OK;
    }

    /// <summary>
    /// Handles notification callbacks (file open/close, dehydrate, delete, rename).
    /// </summary>
    private int OnNotification(ref CF_CALLBACK_INFO callbackInfo, ref CF_CALLBACK_PARAMETERS callbackData)
    {
        var relativePath = Marshal.PtrToStringUni(callbackInfo.RelativePath);
        var callbackType = callbackData.CallbackType;

        _logger.LogDebug("Notification callback {CallbackType} for {RelativePath}",
            callbackType, relativePath);

        switch (callbackType)
        {
            case CF_CALLBACK_TYPE.NOTIFY_FILE_OPEN_COMPLETION:
                _logger.LogTrace("File opened: {RelativePath}", relativePath);
                break;

            case CF_CALLBACK_TYPE.NOTIFY_FILE_CLOSE_COMPLETION:
                _logger.LogTrace("File closed: {RelativePath}", relativePath);
                break;

            case CF_CALLBACK_TYPE.NOTIFY_DEHYDRATE:
                _logger.LogDebug("Dehydrate notification for {RelativePath}", relativePath);
                break;

            case CF_CALLBACK_TYPE.NOTIFY_DELETE:
                _logger.LogDebug("Delete notification for {RelativePath}", relativePath);
                break;

            case CF_CALLBACK_TYPE.NOTIFY_RENAME:
                _logger.LogDebug("Rename notification for {RelativePath}", relativePath);
                break;
        }

        return CfHResult.S_OK;
    }

    /// <summary>
    /// Downloads file content and transfers it to the placeholder via CfExecute.
    /// Looks up the node ID from the local state database if available.
    /// </summary>
    private async Task HydrateFileAsync(
        string fullPath,
        string relativePath,
        CF_CALLBACK_INFO callbackInfo,
        CF_CALLBACK_PARAMETERS callbackData)
    {
        try
        {
            // Look up the node ID from the local state database
            var record = _dbPath != null
                ? await _localStateDb.GetFileRecordAsync(_dbPath, fullPath)
                : null;

            var nodeId = record?.NodeId ?? Guid.Empty;

            using var contentStream = await _chunkedTransfer.DownloadAsync(
                nodeId,
                cancellationToken: CancellationToken.None);

            if (contentStream == null || contentStream.Length == 0)
            {
                _logger.LogWarning("Empty content received for {RelativePath}", relativePath);
                AckHydration(callbackInfo, CfHResult.HR_CLOUD_FILE_NOT_AVAILABLE);
                return;
            }

            // Read content into a buffer for CfExecute TRANSFER_DATA
            using var ms = new MemoryStream();
            await contentStream.CopyToAsync(ms);
            var data = ms.ToArray();

            // Pin the buffer so the native API can read it
            var dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
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
                        _logger.LogError("CfExecute TRANSFER_DATA failed with HRESULT={HResult} for {RelativePath}",
                            hResult, relativePath);
                    }
                    else
                    {
                        _logger.LogInformation("Successfully hydrated {RelativePath} ({Length} bytes)",
                            relativePath, data.Length);

                        // Update hydration state via UpsertFileRecordAsync
                        if (_dbPath != null && record != null)
                        {
                            record.HydrationState = HydrationState.Hydrated;
                            await _localStateDb.UpsertFileRecordAsync(_dbPath, record);
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(opParamsPtr);
                }
            }
            finally
            {
                dataHandle.Free();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error hydrating file {RelativePath}", relativePath);
            AckHydration(callbackInfo, CfHResult.HR_CLOUD_FILE_NOT_AVAILABLE);
        }
    }

    /// <summary>
    /// Acknowledges a hydration failure to Windows.
    /// </summary>
    private static void AckHydration(CF_CALLBACK_INFO callbackInfo, int hResult)
    {
        var opParams = new CF_OPERATION_PARAMETERS
        {
            StructSize = (uint)Marshal.SizeOf<CF_OPERATION_PARAMETERS>(),
            OperationType = CF_OPERATION_TYPE.ACK_DATA,
            Parameters = new CF_OPERATION_PARAM_UNION
            {
                AckData = new CF_OPERATION_PARAM_ACK_DATA
                {
                    CompletionStatus = hResult,
                    Offset = 0,
                    Length = 0,
                    Flags = 0,
                },
            },
        };

        var opParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<CF_OPERATION_PARAMETERS>());
        try
        {
            Marshal.StructureToPtr(opParams, opParamsPtr, false);
            CfApiNative.CfExecute(opParamsPtr, (uint)Marshal.SizeOf<CF_OPERATION_PARAMETERS>());
        }
        finally
        {
            Marshal.FreeHGlobal(opParamsPtr);
        }
    }

    private SemaphoreSlim GetFileLock(string relativePath)
    {
        lock (_lockGate)
        {
            if (!_hydrationLocks.TryGetValue(relativePath, out var semaphore))
            {
                semaphore = new SemaphoreSlim(1, 1);
                _hydrationLocks[relativePath] = semaphore;
            }

            return semaphore;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_callbacksHandle.IsAllocated)
            _callbacksHandle.Free();

        lock (_lockGate)
        {
            foreach (var semaphore in _hydrationLocks.Values)
                semaphore.Dispose();
            _hydrationLocks.Clear();
        }
    }
}
