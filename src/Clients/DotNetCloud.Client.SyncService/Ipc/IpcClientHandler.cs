using System.Text;
using System.Text.Json;
using DotNetCloud.Client.SyncService.ContextManager;
using Microsoft.Extensions.Logging;

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

    private readonly Stream _stream;
    private readonly ISyncContextManager _contextManager;
    private readonly ILogger _logger;
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;

    // Serialises writes from both the command-response path and async event handlers
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private bool _subscribed;
    private EventHandler<SyncProgressEventArgs>? _progressHandler;
    private EventHandler<SyncCompleteEventArgs>? _completeHandler;
    private EventHandler<SyncErrorEventArgs>? _errorHandler;
    private EventHandler<SyncConflictDetectedEventArgs>? _conflictHandler;

    /// <summary>Initializes a new <see cref="IpcClientHandler"/>.</summary>
    public IpcClientHandler(Stream stream, ISyncContextManager contextManager, ILogger logger)
    {
        _stream = stream;
        _contextManager = contextManager;
        _logger = logger;
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

            default:
                await SendErrorAsync(command.Command, $"Unknown command: '{command.Command}'.", cancellationToken);
                break;
        }
    }

    // ── Command handlers ─────────────────────────────────────────────────

    private async Task HandleListContextsAsync(CancellationToken cancellationToken)
    {
        var contexts = await _contextManager.GetContextsAsync();
        var infos = new List<ContextInfo>(contexts.Count);

        foreach (var ctx in contexts)
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
        };

        var registration = await _contextManager.AddContextAsync(request, cancellationToken);
        await SendResponseAsync(command.Command,
            new { contextId = registration.Id, displayName = registration.DisplayName },
            cancellationToken);
    }

    private async Task HandleRemoveAccountAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        if (command.ContextId is null)
        {
            await SendErrorAsync(command.Command, "Missing 'contextId'.", cancellationToken);
            return;
        }

        await _contextManager.RemoveContextAsync(command.ContextId.Value, cancellationToken);
        await SendResponseAsync(command.Command, new { removed = true }, cancellationToken);
    }

    private async Task HandleGetStatusAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        if (command.ContextId is null)
        {
            await SendErrorAsync(command.Command, "Missing 'contextId'.", cancellationToken);
            return;
        }

        var status = await _contextManager.GetStatusAsync(command.ContextId.Value, cancellationToken);
        if (status is null)
        {
            await SendErrorAsync(command.Command,
                $"Context '{command.ContextId}' not found.", cancellationToken);
            return;
        }

        await SendResponseAsync(command.Command, command.ContextId.Value, new
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
        if (command.ContextId is null)
        {
            await SendErrorAsync(command.Command, "Missing 'contextId'.", cancellationToken);
            return;
        }

        await _contextManager.PauseAsync(command.ContextId.Value, cancellationToken);
        await SendResponseAsync(command.Command, new { paused = true }, cancellationToken);
    }

    private async Task HandleResumeAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        if (command.ContextId is null)
        {
            await SendErrorAsync(command.Command, "Missing 'contextId'.", cancellationToken);
            return;
        }

        await _contextManager.ResumeAsync(command.ContextId.Value, cancellationToken);
        await SendResponseAsync(command.Command, new { resumed = true }, cancellationToken);
    }

    private async Task HandleSyncNowAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        if (command.ContextId is null)
        {
            await SendErrorAsync(command.Command, "Missing 'contextId'.", cancellationToken);
            return;
        }

        // Fire and forget — client receives confirmation immediately;
        // progress is delivered via events if the client is subscribed.
        _ = Task.Run(
            () => _contextManager.SyncNowAsync(command.ContextId.Value, cancellationToken),
            cancellationToken);

        await SendResponseAsync(command.Command, new { started = true }, cancellationToken);
    }

    // ── Event subscription ────────────────────────────────────────────────

    private void Subscribe()
    {
        if (_subscribed) return;
        _subscribed = true;

        _progressHandler = (_, args) =>
            _ = PushEventAsync(IpcEvents.SyncProgress, args.ContextId, new
            {
                state = args.Status.State.ToString(),
                pendingUploads = args.Status.PendingUploads,
                pendingDownloads = args.Status.PendingDownloads,
            });

        _completeHandler = (_, args) =>
            _ = PushEventAsync(IpcEvents.SyncComplete, args.ContextId, new
            {
                lastSyncedAt = args.Status.LastSyncedAt,
                conflicts = args.Status.Conflicts,
            });

        _errorHandler = (_, args) =>
            _ = PushEventAsync(IpcEvents.Error, args.ContextId, new
            {
                error = args.ErrorMessage,
            });

        _conflictHandler = (_, args) =>
            _ = PushEventAsync(IpcEvents.ConflictDetected, args.ContextId, new
            {
                originalPath = args.OriginalPath,
                conflictCopyPath = args.ConflictCopyPath,
            });

        _contextManager.SyncProgress += _progressHandler;
        _contextManager.SyncComplete += _completeHandler;
        _contextManager.SyncError += _errorHandler;
        _contextManager.ConflictDetected += _conflictHandler;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        _subscribed = false;

        if (_progressHandler is not null) _contextManager.SyncProgress -= _progressHandler;
        if (_completeHandler is not null) _contextManager.SyncComplete -= _completeHandler;
        if (_errorHandler is not null) _contextManager.SyncError -= _errorHandler;
        if (_conflictHandler is not null) _contextManager.ConflictDetected -= _conflictHandler;
    }

    private async Task PushEventAsync(string eventName, Guid contextId, object data)
    {
        try
        {
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
}
