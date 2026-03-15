using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// Listens to Server-Sent Events (SSE) from <c>/api/v1/files/sync/stream</c> for real-time
/// sync change notifications. Falls back to polling when SSE is unavailable.
/// </summary>
public sealed class SyncStreamListener : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<SyncStreamListener> _logger;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _disposed;

    /// <summary>Access token for authenticated requests.</summary>
    public string? AccessToken { get; set; }

    /// <summary>Fires when the server notifies that new changes are available.</summary>
    public event EventHandler<SyncChangedEventArgs>? SyncChanged;

    /// <summary>Whether an SSE connection is currently active.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>Initializes a new <see cref="SyncStreamListener"/>.</summary>
    public SyncStreamListener(HttpClient http, ILogger<SyncStreamListener> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Starts listening for SSE notifications. Automatically reconnects on failure
    /// with exponential backoff.
    /// </summary>
    public void Start(CancellationToken cancellationToken = default)
    {
        if (_listenTask is not null)
            return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listenTask = ListenLoopAsync(_cts.Token);
    }

    /// <summary>Stops the SSE listener.</summary>
    public async Task StopAsync()
    {
        if (_cts is not null)
            await _cts.CancelAsync();

        if (_listenTask is not null)
        {
            try { await _listenTask; }
            catch (OperationCanceledException) { }
        }

        IsConnected = false;
        _listenTask = null;
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        var attempt = 0;
        const int maxBackoffSeconds = 60;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndListenAsync(cancellationToken);
                // Clean disconnect — reset backoff
                attempt = 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                attempt++;
                var backoff = Math.Min((int)Math.Pow(2, attempt), maxBackoffSeconds);
                _logger.LogWarning(ex,
                    "SSE connection failed (attempt {Attempt}). Reconnecting in {Backoff}s.",
                    attempt, backoff);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(backoff), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        IsConnected = false;
    }

    private async Task ConnectAndListenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/files/sync/stream");
        if (AccessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        IsConnected = true;
        _logger.LogInformation("SSE stream connected.");

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? eventType = null;
        string? dataBuffer = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
            {
                // Stream ended
                _logger.LogInformation("SSE stream ended by server.");
                IsConnected = false;
                return;
            }

            if (line.Length == 0)
            {
                // Empty line = end of event — dispatch if we have data
                if (eventType == "sync-changed" && dataBuffer is not null)
                {
                    ProcessSyncChangedEvent(dataBuffer);
                }

                eventType = null;
                dataBuffer = null;
                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventType = line[6..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                dataBuffer = line[5..].Trim();
            }
            // Lines starting with ':' are comments (keepalives) — ignore
        }
    }

    private void ProcessSyncChangedEvent(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            var root = doc.RootElement;

            long? latestSequence = null;
            if (root.TryGetProperty("latestSequence", out var seqProp))
                latestSequence = seqProp.GetInt64();

            _logger.LogDebug("SSE sync-changed notification received: sequence={Sequence}.", latestSequence);

            SyncChanged?.Invoke(this, new SyncChangedEventArgs
            {
                LatestSequence = latestSequence,
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse SSE sync-changed event data: {Data}.", data);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync();
        _cts?.Dispose();
    }
}

/// <summary>
/// Event args raised when an SSE <c>sync-changed</c> notification is received.
/// </summary>
public sealed class SyncChangedEventArgs : EventArgs
{
    /// <summary>The latest sync sequence on the server, or null if not provided.</summary>
    public long? LatestSequence { get; init; }
}
