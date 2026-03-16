using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.SyncService.Ipc;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.Ipc;

/// <summary>
/// Connects to the background <c>DotNetCloud.Client.SyncService</c> over Named Pipe
/// (Windows) or Unix domain socket (Linux), sends JSON commands, and dispatches
/// push events to the rest of the application.
/// </summary>
public sealed class IpcClient : IIpcClient, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Delay between reconnection attempts when the SyncService is unavailable.</summary>
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly ILogger<IpcClient> _logger;
    private readonly Func<CancellationToken, Task<Stream>>? _transportFactory;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // Tracks pending command requests awaiting responses from the read loop.
    private readonly ConcurrentDictionary<string, TaskCompletionSource<IpcMessage>> _pendingCommands = new();

    private Stream? _stream;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private bool _connected;

    /// <inheritdoc/>
    public event EventHandler<SyncProgressEventData>? SyncProgressReceived;

    /// <inheritdoc/>
    public event EventHandler<SyncCompleteEventData>? SyncCompleteReceived;

    /// <inheritdoc/>
    public event EventHandler<SyncErrorEventData>? SyncErrorReceived;

    /// <inheritdoc/>
    public event EventHandler<SyncConflictEventData>? ConflictDetected;

    /// <inheritdoc/>
    public event EventHandler<ConflictAutoResolvedEventData>? ConflictAutoResolved;

    /// <inheritdoc/>
    public event EventHandler<TransferProgressEventData>? TransferProgressReceived;

    /// <inheritdoc/>
    public event EventHandler<TransferCompleteEventData>? TransferCompleteReceived;

    /// <inheritdoc/>
    public event EventHandler<bool>? ConnectionStateChanged;

    /// <inheritdoc/>
    public bool IsConnected => _connected;

    /// <summary>
    /// Initializes a new <see cref="IpcClient"/> using the default Named Pipe / Unix
    /// socket transport.
    /// </summary>
    public IpcClient(ILogger<IpcClient> logger)
    {
        _logger = logger;
        _transportFactory = null;
    }

    /// <summary>
    /// Initializes a new <see cref="IpcClient"/> with a custom transport factory.
    /// Intended for unit testing.
    /// </summary>
    /// <param name="transportFactory">
    /// Factory that returns a connected bidirectional stream when invoked.
    /// </param>
    /// <param name="logger">Logger instance.</param>
    internal IpcClient(Func<CancellationToken, Task<Stream>> transportFactory, ILogger<IpcClient> logger)
    {
        _logger = logger;
        _transportFactory = transportFactory;
    }

    // ── Connection management ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("IPC connect loop starting.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Attempting to connect to SyncService IPC endpoint.");
                await OpenConnectionAsync(cancellationToken);
                SetConnected(true);

                // Start the read loop first so command responses can be dispatched.
                var readLoopTask = ReadLoopAsync(cancellationToken);

                // Subscribe to push events, then continue reading until disconnected.
                await SubscribeAsync(cancellationToken);
                await readLoopTask;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or EndOfStreamException or SocketException)
            {
                _logger.LogWarning("Disconnected from SyncService: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in IPC client connection.");
            }
            finally
            {
                SetConnected(false);
                CloseConnection();
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Reconnecting to SyncService in {Delay}.", ReconnectDelay);
                await Task.Delay(ReconnectDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogInformation("IPC connect loop stopped.");
    }

    private async Task OpenConnectionAsync(CancellationToken cancellationToken)
    {
        CloseConnection();

        if (_transportFactory is not null)
        {
            _stream = await _transportFactory(cancellationToken);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogInformation("Connecting to SyncService via named pipe '{Pipe}'.", IpcServer.PipeName);

            var pipe = new NamedPipeClientStream(
                ".", IpcServer.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            // Guard against ConnectAsync hanging unexpectedly on some environments.
            var connectTask = pipe.ConnectAsync(cancellationToken);
            await connectTask.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
            _stream = pipe;
            _logger.LogInformation("Named pipe handshake complete.");
        }
        else
        {
            _logger.LogDebug("Connecting to SyncService via Unix socket '{Path}'.", IpcServer.UnixSocketPath);

            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(IpcServer.UnixSocketPath), cancellationToken);
            _stream = new NetworkStream(socket, ownsSocket: true);
        }

        _writer = new StreamWriter(_stream, Utf8NoBom, leaveOpen: true) { AutoFlush = true };
        _reader = new StreamReader(_stream, Utf8NoBom, leaveOpen: true);

        _logger.LogInformation("IPC reader/writer initialized.");

        _logger.LogInformation("Connected to SyncService.");
    }

    private void CloseConnection()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _stream?.Dispose();
        _writer = null;
        _reader = null;
        _stream = null;
    }

    private void SetConnected(bool connected)
    {
        if (_connected == connected) return;
        _connected = connected;
        ConnectionStateChanged?.Invoke(this, connected);
        _logger.LogDebug("IPC connection state: {State}.", connected ? "Connected" : "Disconnected");
    }

    // ── Subscription & read loop ──────────────────────────────────────────

    private async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        var cmd = new IpcCommand { Command = IpcCommands.Subscribe };

        using var subscribeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        subscribeCts.CancelAfter(TimeSpan.FromSeconds(5));

        var response = await SendAndReceiveAsync(cmd, subscribeCts.Token);
        if (response?.Success != true)
        {
            var err = response?.Error ?? "No subscribe response";
            _logger.LogWarning("Subscribe failed: {Error}", err);
            throw new IOException($"Subscribe failed: {err}");
        }

        _logger.LogInformation("Subscribed to SyncService IPC events.");
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await _reader!.ReadLineAsync(cancellationToken);
            if (line is null) return; // Server closed the connection.

            if (string.IsNullOrWhiteSpace(line)) continue;

            DispatchMessage(line);
        }
    }

    // ── Message dispatch ─────────────────────────────────────────────────

    private void DispatchMessage(string json)
    {
        IpcMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<IpcMessage>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed IPC message from SyncService.");
            return;
        }

        if (message is null)
            return;

        // Response messages complete pending command tasks.
        if (message.Type == "response" && message.Command is not null)
        {
            if (_pendingCommands.TryRemove(message.Command, out var tcs))
            {
                tcs.SetResult(message);
            }
            return;
        }

        // Event messages are dispatched to registered event handlers.
        if (message.Type != "event" || message.Event is null)
            return;

        var contextId = message.ContextId ?? Guid.Empty;

        switch (message.Event)
        {
            case IpcEvents.SyncProgress:
                var progress = ParseData<SyncProgressPayload>(message.Data);
                if (progress is not null)
                {
                    SyncProgressReceived?.Invoke(this, new SyncProgressEventData
                    {
                        ContextId = contextId,
                        State = progress.State ?? "Unknown",
                        PendingUploads = progress.PendingUploads,
                        PendingDownloads = progress.PendingDownloads,
                    });
                }
                break;

            case IpcEvents.SyncComplete:
                var complete = ParseData<SyncCompletePayload>(message.Data);
                if (complete is not null)
                {
                    SyncCompleteReceived?.Invoke(this, new SyncCompleteEventData
                    {
                        ContextId = contextId,
                        LastSyncedAt = complete.LastSyncedAt,
                        Conflicts = complete.Conflicts,
                    });
                }
                break;

            case IpcEvents.Error:
                var error = ParseData<SyncErrorPayload>(message.Data);
                if (error is not null)
                {
                    SyncErrorReceived?.Invoke(this, new SyncErrorEventData
                    {
                        ContextId = contextId,
                        Error = error.Error ?? "Unknown error",
                    });
                }
                break;

            case IpcEvents.ConflictDetected:
                var conflict = ParseData<ConflictPayload>(message.Data);
                if (conflict is not null)
                {
                    ConflictDetected?.Invoke(this, new SyncConflictEventData
                    {
                        ContextId = contextId,
                        OriginalPath = conflict.OriginalPath ?? string.Empty,
                        ConflictCopyPath = conflict.ConflictCopyPath ?? string.Empty,
                    });
                }
                break;

            case IpcEvents.TransferProgress:
                var xferProgress = ParseData<TransferProgressPayload>(message.Data);
                if (xferProgress is not null)
                {
                    TransferProgressReceived?.Invoke(this, new TransferProgressEventData
                    {
                        ContextId = contextId,
                        FileName = xferProgress.FileName ?? string.Empty,
                        Direction = xferProgress.Direction ?? "upload",
                        BytesTransferred = xferProgress.BytesTransferred,
                        TotalBytes = xferProgress.TotalBytes,
                        ChunksCompleted = xferProgress.ChunksCompleted,
                        ChunksTotal = xferProgress.ChunksTotal,
                        PercentComplete = xferProgress.PercentComplete,
                    });
                }
                break;

            case IpcEvents.TransferComplete:
                var xferComplete = ParseData<TransferCompletePayload>(message.Data);
                if (xferComplete is not null)
                {
                    TransferCompleteReceived?.Invoke(this, new TransferCompleteEventData
                    {
                        ContextId = contextId,
                        FileName = xferComplete.FileName ?? string.Empty,
                        Direction = xferComplete.Direction ?? "upload",
                        TotalBytes = xferComplete.TotalBytes,
                    });
                }
                break;

            case IpcEvents.ConflictAutoResolved:
                var autoResolved = ParseData<ConflictAutoResolvedPayload>(message.Data);
                if (autoResolved is not null)
                {
                    ConflictAutoResolved?.Invoke(this, new ConflictAutoResolvedEventData
                    {
                        ContextId = contextId,
                        LocalPath = autoResolved.LocalPath,
                        Strategy = autoResolved.Strategy,
                        Resolution = autoResolved.Resolution,
                    });
                }
                break;

            default:
                _logger.LogDebug("Unrecognised IPC event: '{Event}'.", message.Event);
                break;
        }
    }

    private static T? ParseData<T>(object? data)
    {
        if (data is null) return default;

        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ContextInfo>> ListContextsAsync(CancellationToken cancellationToken = default)
    {
        if (!_connected) return [];

        var response = await SendAndReceiveAsync(
            new IpcCommand { Command = IpcCommands.ListContexts }, cancellationToken);

        if (response is null)
        {
            _logger.LogWarning("ListContexts: no response from SyncService.");
            return [];
        }

        if (!response.Success)
        {
            _logger.LogWarning("ListContexts: service returned error: {Error}.", response.Error);
            return [];
        }

        if (response.Data is not null)
        {
            var json = JsonSerializer.Serialize(response.Data, JsonOptions);
            var result = JsonSerializer.Deserialize<List<ContextInfo>>(json, JsonOptions) ?? [];
            _logger.LogInformation("ListContexts: deserialized {Count} context(s).", result.Count);
            return result;
        }

        _logger.LogWarning("ListContexts: success but no data payload.");
        return [];
    }

    /// <inheritdoc/>
    public Task SyncNowAsync(Guid contextId, CancellationToken cancellationToken = default)
        => SendAndReceiveAsync(new IpcCommand { Command = IpcCommands.SyncNow, ContextId = contextId }, cancellationToken);

    /// <inheritdoc/>
    public Task PauseAsync(Guid contextId, CancellationToken cancellationToken = default)
        => SendAndReceiveAsync(new IpcCommand { Command = IpcCommands.Pause, ContextId = contextId }, cancellationToken);

    /// <inheritdoc/>
    public Task ResumeAsync(Guid contextId, CancellationToken cancellationToken = default)
        => SendAndReceiveAsync(new IpcCommand { Command = IpcCommands.Resume, ContextId = contextId }, cancellationToken);

    /// <inheritdoc/>
    public Task RemoveAccountAsync(Guid contextId, CancellationToken cancellationToken = default)
        => SendAndReceiveAsync(new IpcCommand { Command = IpcCommands.RemoveAccount, ContextId = contextId }, cancellationToken);

    /// <inheritdoc/>
    public async Task AddAccountAsync(AddAccountData data, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToElement(data, JsonOptions);
        await SendAndReceiveAsync(
            new IpcCommand { Command = IpcCommands.AddAccount, Data = payload },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConflictRecordData>> ListConflictsAsync(
        Guid contextId, bool includeHistory = false, CancellationToken cancellationToken = default)
    {
        if (!_connected) return [];

        var data = JsonSerializer.SerializeToElement(
            new ListConflictsData { IncludeHistory = includeHistory }, JsonOptions);

        var response = await SendAndReceiveAsync(
            new IpcCommand { Command = IpcCommands.ListConflicts, ContextId = contextId, Data = data },
            cancellationToken);

        if (response?.Success is true && response.Data is not null)
        {
            var json = JsonSerializer.Serialize(response.Data, JsonOptions);
            var payloads = JsonSerializer.Deserialize<List<ConflictRecordPayload>>(json, JsonOptions);
            if (payloads is null) return [];

            return payloads.Select(p => new ConflictRecordData
            {
                Id = p.Id,
                ContextId = contextId,
                OriginalPath = p.OriginalPath,
                ConflictCopyPath = p.ConflictCopyPath,
                NodeId = p.NodeId,
                LocalModifiedAt = p.LocalModifiedAt,
                RemoteModifiedAt = p.RemoteModifiedAt,
                DetectedAt = p.DetectedAt,
                ResolvedAt = p.ResolvedAt,
                Resolution = p.Resolution,
                BaseContentHash = p.BaseContentHash,
                AutoResolved = p.AutoResolved,
            }).ToList();
        }

        return [];
    }

    /// <inheritdoc/>
    public async Task ResolveConflictAsync(
        Guid contextId, int conflictId, string resolution, CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.SerializeToElement(
            new ResolveConflictData { ConflictId = conflictId, Resolution = resolution }, JsonOptions);

        await SendAndReceiveAsync(
            new IpcCommand { Command = IpcCommands.ResolveConflict, ContextId = contextId, Data = data },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateBandwidthAsync(decimal uploadLimitKbps, decimal downloadLimitKbps, CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.SerializeToElement(
            new BandwidthData { UploadLimitKbps = uploadLimitKbps, DownloadLimitKbps = downloadLimitKbps },
            JsonOptions);

        await SendAndReceiveAsync(
            new IpcCommand { Command = IpcCommands.UpdateBandwidth, Data = data },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateConflictSettingsAsync(
        bool autoResolveEnabled, int newerWinsThresholdMinutes, List<string> enabledStrategies,
        CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.SerializeToElement(
            new ConflictSettingsData
            {
                AutoResolveEnabled = autoResolveEnabled,
                NewerWinsThresholdMinutes = newerWinsThresholdMinutes,
                EnabledStrategies = enabledStrategies,
            },
            JsonOptions);

        await SendAndReceiveAsync(
            new IpcCommand { Command = IpcCommands.UpdateConflictSettings, Data = data },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SyncTreeNodeResponse?> GetFolderTreeAsync(Guid contextId, CancellationToken cancellationToken = default)
    {
        if (!_connected) return null;

        var response = await SendAndReceiveAsync(
            new IpcCommand { Command = IpcCommands.GetFolderTree, ContextId = contextId },
            cancellationToken);

        if (response?.Success is true && response.Data is not null)
        {
            var json = JsonSerializer.Serialize(response.Data, JsonOptions);
            return JsonSerializer.Deserialize<SyncTreeNodeResponse>(json, JsonOptions);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task UpdateSelectiveSyncAsync(
        Guid contextId,
        IReadOnlyList<SelectiveSyncRule> rules,
        CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.SerializeToElement(
            new SelectiveSyncRulesData { Rules = rules.ToList() },
            JsonOptions);

        await SendAndReceiveAsync(
            new IpcCommand { Command = IpcCommands.UpdateSelectiveSync, ContextId = contextId, Data = data },
            cancellationToken);
    }

    // ── Send helpers ──────────────────────────────────────────────────────

    private async Task<IpcMessage?> SendAndReceiveAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        if (!_connected || _writer is null || command.Command is null) return null;

        // Register a TaskCompletionSource before sending so the read loop can complete it.
        var tcs = new TaskCompletionSource<IpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingCommands.TryAdd(command.Command, tcs))
        {
            if (_pendingCommands.TryGetValue(command.Command, out var existingTcs))
            {
                _logger.LogDebug("Joining existing pending command '{Command}'.", command.Command);
                return await existingTcs.Task.WaitAsync(cancellationToken);
            }

            _logger.LogWarning("Duplicate command '{Command}' already pending.", command.Command);
            return null;
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(command, JsonOptions);
            await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        }
        catch
        {
            _pendingCommands.TryRemove(command.Command, out _);
            throw;
        }
        finally
        {
            _sendLock.Release();
        }

        // Wait for the read loop to dispatch the response.
        using var registration = cancellationToken.Register(() => 
            _pendingCommands.TryRemove(command.Command!, out _));
        
        try
        {
            return await tcs.Task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _pendingCommands.TryRemove(command.Command, out _);
            throw;
        }
    }

    private async Task SendCommandAsync(IpcCommand command, CancellationToken cancellationToken)
    {
        if (_writer is null) return;

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(command, JsonOptions);
            await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        // Cancel all pending command tasks.
        foreach (var (cmd, tcs) in _pendingCommands)
        {
            tcs.TrySetCanceled();
        }
        _pendingCommands.Clear();

        CloseConnection();
        _sendLock.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Private payload types (IPC response deserialization) ──────────────

    private sealed class SyncProgressPayload
    {
        public string? State { get; init; }
        public int PendingUploads { get; init; }
        public int PendingDownloads { get; init; }
    }

    private sealed class SyncCompletePayload
    {
        public DateTime? LastSyncedAt { get; init; }
        public int Conflicts { get; init; }
    }

    private sealed class SyncErrorPayload
    {
        public string? Error { get; init; }
    }

    private sealed class ConflictPayload
    {
        public string? OriginalPath { get; init; }
        public string? ConflictCopyPath { get; init; }
    }

    private sealed class TransferProgressPayload
    {
        public string? FileName { get; init; }
        public string? Direction { get; init; }
        public long BytesTransferred { get; init; }
        public long TotalBytes { get; init; }
        public int ChunksCompleted { get; init; }
        public int ChunksTotal { get; init; }
        public double PercentComplete { get; init; }
    }

    private sealed class TransferCompletePayload
    {
        public string? FileName { get; init; }
        public string? Direction { get; init; }
        public long TotalBytes { get; init; }
    }

    private sealed class ConflictAutoResolvedPayload
    {
        public string? LocalPath { get; init; }
        public string? Strategy { get; init; }
        public string? Resolution { get; init; }
    }
}
