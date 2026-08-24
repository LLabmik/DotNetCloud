using System.Text.Json;
using System.Text.Json.Serialization;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Calendar;
using DotNetCloud.Client.Android.Chat;
using DotNetCloud.Client.Android.Notes;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// <see cref="IOfflineSyncService"/> implementation that replays queued operations
/// (chat messages, notes, calendar events) against the appropriate REST client.
/// Operations are delivered in priority order (chat first) and removed on success;
/// flushing stops at the first failure so ordering is preserved within a batch.
/// </summary>
internal sealed class OfflineSyncService : IOfflineSyncService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IOfflineOperationQueue _queue;
    private readonly IConnectivityMonitor _connectivity;
    private readonly IServerReachabilityService _reachability;
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly IChatRestClient _chatApi;
    private readonly INotesRestClient _notesApi;
    private readonly ICalendarRestClient _calendarApi;
    private readonly ILogger<OfflineSyncService> _logger;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private System.Threading.Timer? _periodicFlushTimer;
    private bool _started;

    /// <summary>Initializes a new <see cref="OfflineSyncService"/>.</summary>
    public OfflineSyncService(
        IOfflineOperationQueue queue,
        IConnectivityMonitor connectivity,
        IServerReachabilityService reachability,
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        IChatRestClient chatApi,
        INotesRestClient notesApi,
        ICalendarRestClient calendarApi,
        ILogger<OfflineSyncService> logger)
    {
        _queue = queue;
        _connectivity = connectivity;
        _reachability = reachability;
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _chatApi = chatApi;
        _notesApi = notesApi;
        _calendarApi = calendarApi;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken ct = default)
    {
        if (_started)
            return Task.CompletedTask;
        _started = true;

        _connectivity.ConnectivityRestored += OnConnectivityRestored;
        _connectivity.Start();

        // Flush queued operations when the server becomes reachable again
        // (distinct from device connectivity — the phone can have internet
        // while the server is down).
        _reachability.AvailabilityChanged += OnServerAvailabilityChanged;
        _reachability.Start();

        // Periodic retry so queued operations flush even without a connectivity
        // change or SignalR reconnect.
        _periodicFlushTimer = new System.Threading.Timer(
            _ => _ = PeriodicFlushAsync(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));

        // If we're already online (e.g. app launched with a queue from a prior session),
        // flush right away.
        if (_connectivity.IsOnline)
        {
            _ = FlushAllAsync(ct);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<int> FlushAllAsync(CancellationToken ct = default)
    {
        await _flushLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var pending = await _queue.GetAllAsync(ct).ConfigureAwait(false);
            if (pending.Count == 0)
                return 0;

            var (serverUrl, token) = await GetCredentialsAsync(ct).ConfigureAwait(false);
            if (serverUrl is null || token is null)
            {
                _logger.LogDebug("OfflineSync: no active connection/token; deferring flush of {Count} operation(s).", pending.Count);
                return 0;
            }

            _logger.LogInformation("OfflineSync: flushing {Count} queued operation(s).", pending.Count);
            var flushed = new List<long>(pending.Count);
            foreach (var op in pending)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await DispatchAsync(serverUrl, token, op, ct).ConfigureAwait(false);
                    flushed.Add(op.RowId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OfflineSync: failed to flush operation {RowId} ({Type}); will retry later.", op.RowId, op.OperationType);
                    break; // preserve ordering — stop on first failure
                }
            }

            if (flushed.Count > 0)
            {
                await _queue.RemoveAsync(flushed, ct).ConfigureAwait(false);
                _logger.LogInformation("OfflineSync: delivered {Count} operation(s).", flushed.Count);
            }

            return flushed.Count;
        }
        finally
        {
            _flushLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasPendingAsync(CancellationToken ct = default) =>
        await _queue.CountAsync(ct).ConfigureAwait(false) > 0;

    private async void OnConnectivityRestored()
    {
        try
        {
            _logger.LogInformation("OfflineSync: connectivity restored; flushing queued operations.");
            await FlushAllAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OfflineSync: flush after connectivity restore failed.");
        }
    }

    private void OnServerAvailabilityChanged()
    {
        if (!_reachability.IsServerOnline)
            return;

        _logger.LogInformation("OfflineSync: server reachable again; flushing queued operations.");
        _ = FlushAllAsync();
    }

    private async Task PeriodicFlushAsync()
    {
        try
        {
            if (_reachability.IsServerOnline && await _queue.CountAsync().ConfigureAwait(false) > 0)
            {
                _logger.LogDebug("OfflineSync: periodic flush triggered for {Count} queued operation(s).",
                    await _queue.CountAsync().ConfigureAwait(false));
                await FlushAllAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OfflineSync: periodic flush failed (will retry).");
        }
    }

    private async Task DispatchAsync(string serverUrl, string token, QueuedOperation op, CancellationToken ct)
    {
        switch (op.OperationType)
        {
            case OfflineOperationType.ChatMessage:
            {
                var payload = Deserialize<OfflineChatMessagePayload>(op);
                await _chatApi.SendMessageAsync(serverUrl, token, payload.ChannelId, payload.Content, ct).ConfigureAwait(false);
                break;
            }
            case OfflineOperationType.NoteCreate:
            {
                var payload = Deserialize<OfflineNoteCreatePayload>(op);
                await _notesApi.CreateNoteAsync(serverUrl, token, payload.Dto, ct).ConfigureAwait(false);
                break;
            }
            case OfflineOperationType.NoteUpdate:
            {
                var payload = Deserialize<OfflineNoteUpdatePayload>(op);
                await _notesApi.UpdateNoteAsync(serverUrl, token, payload.NoteId, payload.Dto, ct).ConfigureAwait(false);
                break;
            }
            case OfflineOperationType.NoteDelete:
            {
                var payload = Deserialize<OfflineNoteDeletePayload>(op);
                await _notesApi.DeleteNoteAsync(serverUrl, token, payload.NoteId, ct).ConfigureAwait(false);
                break;
            }
            case OfflineOperationType.CalendarEventCreate:
            {
                var payload = Deserialize<OfflineCalendarEventCreatePayload>(op);
                await _calendarApi.CreateEventAsync(serverUrl, token, payload.Dto, ct).ConfigureAwait(false);
                break;
            }
            case OfflineOperationType.CalendarEventUpdate:
            {
                var payload = Deserialize<OfflineCalendarEventUpdatePayload>(op);
                await _calendarApi.UpdateEventAsync(serverUrl, token, payload.EventId, payload.Dto, ct).ConfigureAwait(false);
                break;
            }
            case OfflineOperationType.CalendarEventDelete:
            {
                var payload = Deserialize<OfflineCalendarEventDeletePayload>(op);
                await _calendarApi.DeleteEventAsync(serverUrl, token, payload.EventId, ct).ConfigureAwait(false);
                break;
            }
            default:
                throw new InvalidOperationException($"Unhandled queued operation type: {op.OperationType}.");
        }
    }

    private static T Deserialize<T>(QueuedOperation op)
    {
        var value = JsonSerializer.Deserialize<T>(op.PayloadJson, JsonOpts)
            ?? throw new InvalidOperationException($"Invalid payload for queued operation {op.RowId} ({op.OperationType}).");
        return value;
    }

    private async Task<(string? ServerUrl, string? Token)> GetCredentialsAsync(CancellationToken ct)
    {
        var connection = _serverStore.GetActive();
        if (connection is null)
            return (null, null);

        var token = await _tokenStore.GetAccessTokenAsync(connection.ServerBaseUrl, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
            return (null, null);

        return (connection.ServerBaseUrl, token);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _periodicFlushTimer?.Dispose();
        _connectivity.ConnectivityRestored -= OnConnectivityRestored;
        _reachability.AvailabilityChanged -= OnServerAvailabilityChanged;
        _flushLock.Dispose();
    }
}
