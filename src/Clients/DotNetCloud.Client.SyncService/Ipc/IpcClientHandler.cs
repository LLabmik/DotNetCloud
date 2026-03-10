using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using DotNetCloud.Client.SyncService.ContextManager;
using Microsoft.Extensions.Logging;
using System.Security.Principal;

namespace DotNetCloud.Client.SyncService.Ipc;

/// <summary>
/// Handles bidirectional communication with a single IPC client.
/// Reads newline-delimited JSON <see cref="IpcCommand"/> messages from the stream
/// and writes <see cref="IpcMessage"/> responses or push events back.
/// </summary>
public sealed class IpcClientHandler : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly TimeSpan SyncNowDebounceWindow = TimeSpan.FromSeconds(5);
    private static readonly ConcurrentDictionary<Guid, DateTimeOffset> LastSyncNowAt = new();

    private readonly Stream _stream;
    private readonly ISyncContextManager _contextManager;
    private readonly ILogger _logger;
    private readonly IpcCallerIdentity _callerIdentity;
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;

    // Serialises writes from both the command-response path and async event handlers
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private bool _subscribed;
    private EventHandler<SyncProgressEventArgs>? _progressHandler;
    private EventHandler<SyncCompleteEventArgs>? _completeHandler;
    private EventHandler<SyncErrorEventArgs>? _errorHandler;
    private EventHandler<SyncConflictDetectedEventArgs>? _conflictHandler;
    private EventHandler<SyncConflictAutoResolvedEventArgs>? _conflictAutoResolvedHandler;
    private EventHandler<ContextTransferProgressEventArgs>? _transferProgressHandler;
    private EventHandler<ContextTransferCompleteEventArgs>? _transferCompleteHandler;
    private readonly HashSet<Guid> _ownedContextIds = [];

    /// <summary>Initializes a new <see cref="IpcClientHandler"/>.</summary>
    public IpcClientHandler(
        Stream stream,
        ISyncContextManager contextManager,
        ILogger logger,
        IpcCallerIdentity? callerIdentity = null)
    {
        _stream = stream;
        _contextManager = contextManager;
        _logger = logger;
        _callerIdentity = callerIdentity ?? IpcCallerIdentity.Unavailable;
        _writer = new StreamWriter(stream, Utf8NoBom, leaveOpen: true) { AutoFlush = true };
        _reader = new StreamReader(stream, Utf8NoBom, leaveOpen: true);
    }

    /// <summary>
    /// Reads and dispatches commands until the client disconnects or
    /// <paramref name="cancellationToken"/> is cancelled.
    /// </summary>
    public async Task HandleAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line is null) break; // Client disconnected cleanly

                if (string.IsNullOrWhiteSpace(line)) continue;

                await DispatchAsync(line, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { /* Remote end closed the pipe/socket */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in IPC client handler.");
        }
        finally
        {
            Unsubscribe();
            await _writer.DisposeAsync();
            _reader.Dispose();
            await _stream.DisposeAsync();
            _writeLock.Dispose();
        }
    }

    // ── Dispatch ─────────────────────────────────────────────────────────

    private async Task DispatchAsync(string line, CancellationToken cancellationToken)
    {
        IpcCommand? command;
        try
        {
            command = JsonSerializer.Deserialize<IpcCommand>(line, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed IPC command received.");
            await SendErrorAsync(null, "Invalid JSON.", cancellationToken);
            return;
        }

        if (command is null)
        {
            await SendErrorAsync(null, "Null command.", cancellationToken);
            return;
        }

        try
        {
            await ExecuteAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing IPC command '{Command}'.", command.Command);
            await SendErrorAsync(command.Command, ex.Message, cancellationToken);
        }
    }

    private async Task ExecuteAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "IPC command received: Command={Command}, ContextId={ContextId}.",
            command.Command,
            command.ContextId);

        switch (command.Command)
        {
            case IpcCommands.ListContexts:
                await HandleListContextsAsync(cancellationToken);
                break;

            case IpcCommands.AddAccount:
                await HandleAddAccountAsync(command, cancellationToken);
                break;

            case IpcCommands.RemoveAccount:
                await HandleRemoveAccountAsync(command, cancellationToken);
                break;

            case IpcCommands.GetStatus:
                await HandleGetStatusAsync(command, cancellationToken);
                break;

            case IpcCommands.Pause:
                await HandlePauseAsync(command, cancellationToken);
                break;

            case IpcCommands.Resume:
                await HandleResumeAsync(command, cancellationToken);
                break;

            case IpcCommands.SyncNow:
                await HandleSyncNowAsync(command, cancellationToken);
                break;

            case IpcCommands.Subscribe:
                Subscribe();
                await SendResponseAsync(command.Command, new { subscribed = true }, cancellationToken);
                break;

            case IpcCommands.Unsubscribe:
                Unsubscribe();
                await SendResponseAsync(command.Command, new { subscribed = false }, cancellationToken);
                break;

            case IpcCommands.ListConflicts:
                await HandleListConflictsAsync(command, cancellationToken);
                break;

            case IpcCommands.ResolveConflict:
                await HandleResolveConflictAsync(command, cancellationToken);
                break;

            case IpcCommands.UpdateBandwidth:
                await HandleUpdateBandwidthAsync(command, cancellationToken);
                break;

            case IpcCommands.UpdateConflictSettings:
                await HandleUpdateConflictSettingsAsync(command, cancellationToken);
                break;

            case IpcCommands.GetFolderTree:
                await HandleGetFolderTreeAsync(command, cancellationToken);
                break;

            default:
                await SendErrorAsync(command.Command, $"Unknown command: '{command.Command}'.", cancellationToken);
                break;
        }
    }

    // ── Command handlers ─────────────────────────────────────────────────

    private async Task HandleListContextsAsync(CancellationToken cancellationToken)
    {
        if (!await EnsureCallerIdentityAsync(IpcCommands.ListContexts, null, cancellationToken))
            return;

        var contexts = await _contextManager.GetContextsAsync();
        var allowedContexts = contexts
            .Where(c => _callerIdentity.MatchesOwner(c.OsUserName))
            .ToList();

        var infos = new List<ContextInfo>(contexts.Count);

        foreach (var ctx in allowedContexts)
        {
            var status = await _contextManager.GetStatusAsync(ctx.Id, cancellationToken);
            infos.Add(new ContextInfo
            {
                Id = ctx.Id,
                DisplayName = ctx.DisplayName,
                ServerBaseUrl = ctx.ServerBaseUrl,
                LocalFolderPath = ctx.LocalFolderPath,
                State = status?.State.ToString() ?? "Unknown",
                PendingUploads = status?.PendingUploads ?? 0,
                PendingDownloads = status?.PendingDownloads ?? 0,
                LastSyncedAt = status?.LastSyncedAt,
                LastError = status?.LastError,
            });
        }

        await SendResponseAsync(IpcCommands.ListContexts, infos, cancellationToken);
    }

    private async Task HandleAddAccountAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        if (command.Data is null)
        {
            await SendErrorAsync(command.Command, "Missing 'data' payload.", cancellationToken);
            return;
        }

        var data = command.Data.Value.Deserialize<AddAccountData>(JsonOptions);
        if (data is null)
        {
            await SendErrorAsync(command.Command, "Invalid 'data' payload.", cancellationToken);
            return;
        }

        var request = new AddAccountRequest
        {
            ServerBaseUrl = data.ServerUrl,
            UserId = data.UserId,
            LocalFolderPath = data.LocalFolderPath,
            DisplayName = data.DisplayName,
            AccessToken = data.AccessToken,
            RefreshToken = data.RefreshToken,
            ExpiresAt = data.ExpiresAt,
            OsUserName = _callerIdentity.AccountName ?? _callerIdentity.NormalizedIdentity ?? string.Empty,
        };

        if (!await EnsureCallerIdentityAsync(command.Command, null, cancellationToken))
            return;

        var addContextResult = await ExecuteWithCallerIdentityAsync(
            command.Command,
            null,
            () => _contextManager.AddContextAsync(request, cancellationToken),
            cancellationToken);

        if (!addContextResult.Success)
            return;

        var registration = addContextResult.Result;

        await SendResponseAsync(command.Command,
            new { contextId = registration.Id, displayName = registration.DisplayName },
            cancellationToken);
    }

    private async Task HandleRemoveAccountAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        var registration = await GetOwnedContextOrRejectAsync(command.Command, command.ContextId, cancellationToken);
        if (registration is null)
        {
            return;
        }

        var removed = await ExecuteWithCallerIdentityAsync(
            command.Command,
            registration.Id,
            () => _contextManager.RemoveContextAsync(registration.Id, cancellationToken),
            cancellationToken);

        if (!removed)
            return;

        await SendResponseAsync(command.Command, new { removed = true }, cancellationToken);
    }

    private async Task HandleGetStatusAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        var registration = await GetOwnedContextOrRejectAsync(command.Command, command.ContextId, cancellationToken);
        if (registration is null)
            return;

        var statusResult = await ExecuteWithCallerIdentityAsync(
            command.Command,
            registration.Id,
            () => _contextManager.GetStatusAsync(registration.Id, cancellationToken),
            cancellationToken);

        if (!statusResult.Success)
            return;

        var status = statusResult.Result;

        if (status is null)
        {
            await SendErrorAsync(command.Command, "Context not found or inaccessible.", cancellationToken);
            return;
        }

        await SendResponseAsync(command.Command, registration.Id, new
        {
            state = status.State.ToString(),
            pendingUploads = status.PendingUploads,
            pendingDownloads = status.PendingDownloads,
            conflicts = status.Conflicts,
            lastSyncedAt = status.LastSyncedAt,
            lastError = status.LastError,
        }, cancellationToken);
    }

    private async Task HandlePauseAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        var registration = await GetOwnedContextOrRejectAsync(command.Command, command.ContextId, cancellationToken);
        if (registration is null)
            return;

        var paused = await ExecuteWithCallerIdentityAsync(
            command.Command,
            registration.Id,
            () => _contextManager.PauseAsync(registration.Id, cancellationToken),
            cancellationToken);

        if (!paused)
            return;

        await SendResponseAsync(command.Command, new { paused = true }, cancellationToken);
    }

    private async Task HandleResumeAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        var registration = await GetOwnedContextOrRejectAsync(command.Command, command.ContextId, cancellationToken);
        if (registration is null)
            return;

        var resumed = await ExecuteWithCallerIdentityAsync(
            command.Command,
            registration.Id,
            () => _contextManager.ResumeAsync(registration.Id, cancellationToken),
            cancellationToken);

        if (!resumed)
            return;

        await SendResponseAsync(command.Command, new { resumed = true }, cancellationToken);
    }

    private async Task HandleSyncNowAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        var registration = await GetOwnedContextOrRejectAsync(command.Command, command.ContextId, cancellationToken);
        if (registration is null)
            return;

        var now = DateTimeOffset.UtcNow;
        if (LastSyncNowAt.TryGetValue(registration.Id, out var lastSyncNowAt)
            && now - lastSyncNowAt < SyncNowDebounceWindow)
        {
            await SendResponseAsync(command.Command, new { started = false, reason = "rate-limited" }, cancellationToken);
            return;
        }

        LastSyncNowAt[registration.Id] = now;

        // Fire and forget — client receives confirmation immediately;
        // progress is delivered via events if the client is subscribed.
        _ = Task.Run(async () =>
        {
            await ExecuteWithCallerIdentityAsync(
                command.Command,
                registration.Id,
                () => _contextManager.SyncNowAsync(registration.Id, cancellationToken),
                cancellationToken);
        }, cancellationToken);

        await SendResponseAsync(command.Command, new { started = true }, cancellationToken);
    }

    private async Task HandleListConflictsAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        var registration = await GetOwnedContextOrRejectAsync(command.Command, command.ContextId, cancellationToken);
        if (registration is null)
            return;

        var includeHistory = false;
        if (command.Data is not null)
        {
            var data = command.Data.Value.Deserialize<ListConflictsData>(JsonOptions);
            includeHistory = data?.IncludeHistory ?? false;
        }

        var recordsResult = await ExecuteWithCallerIdentityAsync(
            command.Command,
            registration.Id,
            () => _contextManager.ListConflictsAsync(registration.Id, includeHistory, cancellationToken),
            cancellationToken);

        if (!recordsResult.Success)
            return;

        var records = recordsResult.Result;

        var payload = records.Select(r => new ConflictRecordPayload
        {
            Id = r.Id,
            OriginalPath = r.OriginalPath,
            ConflictCopyPath = r.ConflictCopyPath,
            NodeId = r.NodeId.ToString(),
            LocalModifiedAt = r.LocalModifiedAt,
            RemoteModifiedAt = r.RemoteModifiedAt,
            DetectedAt = r.DetectedAt,
            ResolvedAt = r.ResolvedAt,
            Resolution = r.Resolution,
            BaseContentHash = r.BaseContentHash,
            AutoResolved = r.AutoResolved,
        }).ToList();

        await SendResponseAsync(command.Command, registration.Id, payload, cancellationToken);
    }

    private async Task HandleResolveConflictAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        var registration = await GetOwnedContextOrRejectAsync(command.Command, command.ContextId, cancellationToken);
        if (registration is null)
            return;

        if (command.Data is null)
        {
            await SendErrorAsync(command.Command, "Missing 'data' payload.", cancellationToken);
            return;
        }

        var data = command.Data.Value.Deserialize<ResolveConflictData>(JsonOptions);
        if (data is null)
        {
            await SendErrorAsync(command.Command, "Invalid 'data' payload.", cancellationToken);
            return;
        }

        var resolved = await ExecuteWithCallerIdentityAsync(
            command.Command,
            registration.Id,
            () => _contextManager.ResolveConflictAsync(
                registration.Id, data.ConflictId, data.Resolution, cancellationToken),
            cancellationToken);

        if (!resolved)
            return;

        await SendResponseAsync(command.Command, new { resolved = true }, cancellationToken);
    }

    private async Task HandleUpdateBandwidthAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        if (!await EnsureCallerIdentityAsync(command.Command, null, cancellationToken))
            return;

        if (command.Data is null)
        {
            await SendErrorAsync(command.Command, "Missing 'data' payload.", cancellationToken);
            return;
        }

        var data = command.Data.Value.Deserialize<BandwidthData>(JsonOptions);
        if (data is null)
        {
            await SendErrorAsync(command.Command, "Invalid 'data' payload.", cancellationToken);
            return;
        }

        await _contextManager.UpdateBandwidthAsync(
            data.UploadLimitKbps, data.DownloadLimitKbps, cancellationToken);

        await SendResponseAsync(command.Command, new { updated = true }, cancellationToken);
    }

    private async Task HandleUpdateConflictSettingsAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        if (!await EnsureCallerIdentityAsync(command.Command, null, cancellationToken))
            return;

        if (command.Data is null)
        {
            await SendErrorAsync(command.Command, "Missing 'data' payload.", cancellationToken);
            return;
        }

        var data = command.Data.Value.Deserialize<ConflictSettingsData>(JsonOptions);
        if (data is null)
        {
            await SendErrorAsync(command.Command, "Invalid 'data' payload.", cancellationToken);
            return;
        }

        var settings = new DotNetCloud.Client.Core.Conflict.ConflictResolutionSettings
        {
            AutoResolveEnabled = data.AutoResolveEnabled,
            NewerWinsThresholdMinutes = data.NewerWinsThresholdMinutes,
            EnabledStrategies = data.EnabledStrategies,
        };

        await _contextManager.PersistConflictResolutionSettingsAsync(settings, cancellationToken);

        await SendResponseAsync(command.Command, new { updated = true }, cancellationToken);
    }

    private async Task HandleGetFolderTreeAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        var registration = await GetOwnedContextOrRejectAsync(command.Command, command.ContextId, cancellationToken);
        if (registration is null)
            return;

        var treeResult = await ExecuteWithCallerIdentityAsync(
            command.Command,
            registration.Id,
            () => _contextManager.GetFolderTreeAsync(registration.Id, cancellationToken),
            cancellationToken);

        if (!treeResult.Success)
            return;

        var tree = treeResult.Result;

        if (tree is null)
        {
            await SendErrorAsync(command.Command, "Context not found or inaccessible.", cancellationToken);
            return;
        }

        await SendResponseAsync(command.Command, tree, cancellationToken);
    }

    // ── Event subscription ────────────────────────────────────────────────

    private void Subscribe()
    {
        if (!_callerIdentity.IsAvailable)
            return;

        if (_subscribed) return;
        _subscribed = true;

        _progressHandler = (_, args) =>
            _ = PushEventIfOwnedAsync(IpcEvents.SyncProgress, args.ContextId, new
            {
                state = args.Status.State.ToString(),
                pendingUploads = args.Status.PendingUploads,
                pendingDownloads = args.Status.PendingDownloads,
            });

        _completeHandler = (_, args) =>
            _ = PushEventIfOwnedAsync(IpcEvents.SyncComplete, args.ContextId, new
            {
                lastSyncedAt = args.Status.LastSyncedAt,
                conflicts = args.Status.Conflicts,
            });

        _errorHandler = (_, args) =>
            _ = PushEventIfOwnedAsync(IpcEvents.Error, args.ContextId, new
            {
                error = args.ErrorMessage,
            });

        _conflictHandler = (_, args) =>
            _ = PushEventIfOwnedAsync(IpcEvents.ConflictDetected, args.ContextId, new
            {
                originalPath = args.OriginalPath,
                conflictCopyPath = args.ConflictCopyPath,
            });

        _conflictAutoResolvedHandler = (_, args) =>
            _ = PushEventIfOwnedAsync(IpcEvents.ConflictAutoResolved, args.ContextId, new ConflictAutoResolvedPayload
            {
                LocalPath = args.LocalPath,
                Strategy = args.Strategy,
                Resolution = args.Resolution,
            });

        _transferProgressHandler = (_, args) =>
            _ = PushEventIfOwnedAsync(IpcEvents.TransferProgress, args.ContextId, new TransferProgressPayload
            {
                FileName = args.FileName,
                Direction = args.Direction,
                BytesTransferred = args.BytesTransferred,
                TotalBytes = args.TotalBytes,
                ChunksCompleted = args.ChunksTransferred,
                ChunksTotal = args.TotalChunks,
                PercentComplete = args.PercentComplete,
            });

        _transferCompleteHandler = (_, args) =>
            _ = PushEventIfOwnedAsync(IpcEvents.TransferComplete, args.ContextId, new TransferCompletePayload
            {
                FileName = args.FileName,
                Direction = args.Direction,
                TotalBytes = args.TotalBytes,
            });

        _contextManager.SyncProgress += _progressHandler;
        _contextManager.SyncComplete += _completeHandler;
        _contextManager.SyncError += _errorHandler;
        _contextManager.ConflictDetected += _conflictHandler;
        _contextManager.ConflictAutoResolved += _conflictAutoResolvedHandler;
        _contextManager.TransferProgress += _transferProgressHandler;
        _contextManager.TransferComplete += _transferCompleteHandler;

        _ = RefreshOwnedContextIdsAsync(CancellationToken.None);
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;

        if (_progressHandler is not null) _contextManager.SyncProgress -= _progressHandler;
        if (_completeHandler is not null) _contextManager.SyncComplete -= _completeHandler;
        if (_errorHandler is not null) _contextManager.SyncError -= _errorHandler;
        if (_conflictHandler is not null) _contextManager.ConflictDetected -= _conflictHandler;
        if (_conflictAutoResolvedHandler is not null) _contextManager.ConflictAutoResolved -= _conflictAutoResolvedHandler;
        if (_transferProgressHandler is not null) _contextManager.TransferProgress -= _transferProgressHandler;
        if (_transferCompleteHandler is not null) _contextManager.TransferComplete -= _transferCompleteHandler;
    }

    private async Task PushEventIfOwnedAsync(string eventName, Guid contextId, object data)
    {
        try
        {
            if (!_callerIdentity.IsAvailable)
                return;

            if (!_ownedContextIds.Contains(contextId))
            {
                await RefreshOwnedContextIdsAsync(CancellationToken.None);
                if (!_ownedContextIds.Contains(contextId))
                    return;
            }

            await SendMessageAsync(new IpcMessage
            {
                Type = "event",
                Event = eventName,
                ContextId = contextId,
                Success = true,
                Data = data,
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not push '{Event}' event — client may have disconnected.", eventName);
        }
    }

    // ── Messaging ─────────────────────────────────────────────────────────

    private async Task SendResponseAsync(string command, object data, CancellationToken cancellationToken) =>
        await SendMessageAsync(new IpcMessage
        {
            Type = "response",
            Command = command,
            Success = true,
            Data = data,
        }, cancellationToken);

    private async Task SendResponseAsync(
        string command, Guid contextId, object data, CancellationToken cancellationToken) =>
        await SendMessageAsync(new IpcMessage
        {
            Type = "response",
            Command = command,
            ContextId = contextId,
            Success = true,
            Data = data,
        }, cancellationToken);

    private async Task SendErrorAsync(string? command, string error, CancellationToken cancellationToken) =>
        await SendMessageAsync(new IpcMessage
        {
            Type = "response",
            Command = command,
            Success = false,
            Error = error,
        }, cancellationToken);

    private async Task SendMessageAsync(IpcMessage message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Unsubscribe();
        return ValueTask.CompletedTask;
    }

    private async Task<bool> EnsureCallerIdentityAsync(string command, Guid? contextId, CancellationToken cancellationToken)
    {
        if (_callerIdentity.IsAvailable)
            return true;

        _logger.LogWarning(
            "IPC command denied due to unavailable caller identity: Command={Command}, ContextId={ContextId}, Caller={Caller}.",
            command,
            contextId,
            _callerIdentity.RawIdentity ?? "<unavailable>");

        await SendErrorAsync(command, "Caller identity unavailable.", cancellationToken);
        return false;
    }

    private async Task<SyncContextRegistration?> GetOwnedContextOrRejectAsync(
        string command,
        Guid? contextId,
        CancellationToken cancellationToken)
    {
        if (contextId is null)
        {
            await SendErrorAsync(command, "Missing 'contextId'.", cancellationToken);
            return null;
        }

        if (!await EnsureCallerIdentityAsync(command, contextId, cancellationToken))
            return null;

        var contexts = await _contextManager.GetContextsAsync();
        var registration = contexts.FirstOrDefault(c => c.Id == contextId.Value);

        if (registration is null || !_callerIdentity.MatchesOwner(registration.OsUserName))
        {
            _logger.LogWarning(
                "IPC command denied due to context ownership mismatch: Command={Command}, ContextId={ContextId}, Caller={Caller}, Owner={Owner}.",
                command,
                contextId,
                _callerIdentity.NormalizedIdentity ?? "<unknown>",
                registration?.OsUserName ?? "<missing>");

            await SendErrorAsync(command, "Context not found or inaccessible.", cancellationToken);
            return null;
        }

        return registration;
    }

    private async Task RefreshOwnedContextIdsAsync(CancellationToken cancellationToken)
    {
        if (!_callerIdentity.IsAvailable)
            return;

        var contexts = await _contextManager.GetContextsAsync();
        _ownedContextIds.Clear();
        foreach (var context in contexts)
        {
            if (_callerIdentity.MatchesOwner(context.OsUserName))
                _ownedContextIds.Add(context.Id);
        }
    }

    private async Task<(bool Success, T Result)> ExecuteWithCallerIdentityAsync<T>(
        string command,
        Guid? contextId,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            return (true, await operation());

        var accessToken = _callerIdentity.WindowsAccessToken;
        if (accessToken is null || accessToken.IsInvalid || accessToken.IsClosed)
            return (true, await operation());

        try
        {
            var result = WindowsIdentity.RunImpersonated(
                accessToken,
                () => operation().GetAwaiter().GetResult());

            return (true, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "IPC command failed during Windows impersonation: Command={Command}, ContextId={ContextId}, Caller={Caller}.",
                command,
                contextId,
                _callerIdentity.NormalizedIdentity ?? "<unknown>");

            await SendErrorAsync(command, "Privilege transition failed.", cancellationToken);
            return (false, default!);
        }
    }

    private async Task<bool> ExecuteWithCallerIdentityAsync(
        string command,
        Guid? contextId,
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            await operation();
            return true;
        }

        var accessToken = _callerIdentity.WindowsAccessToken;
        if (accessToken is null || accessToken.IsInvalid || accessToken.IsClosed)
        {
            await operation();
            return true;
        }

        try
        {
            WindowsIdentity.RunImpersonated(
                accessToken,
                () => operation().GetAwaiter().GetResult());

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "IPC command failed during Windows impersonation: Command={Command}, ContextId={ContextId}, Caller={Caller}.",
                command,
                contextId,
                _callerIdentity.NormalizedIdentity ?? "<unknown>");

            await SendErrorAsync(command, "Privilege transition failed.", cancellationToken);
            return false;
        }
    }
}
