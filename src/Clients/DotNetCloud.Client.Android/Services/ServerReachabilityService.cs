using DotNetCloud.Client.Core.Auth;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Probes the active server's <c>/health/live</c> endpoint periodically.
/// Combines device connectivity and server reachability into a single signal.
/// </summary>
internal sealed class ServerReachabilityService : IServerReachabilityService, IDisposable
{
    private static readonly TimeSpan OnlineInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan OfflineInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly IServerConnectionStore _serverStore;
    private readonly IConnectivityMonitor _connectivity;
    private readonly ILogger<ServerReachabilityService> _logger;
    private readonly HttpClient _http;

    private bool _isServerOnline;
    private bool _started;
    private CancellationTokenSource? _cts;

    /// <summary>Creates a new reachability service.</summary>
    public ServerReachabilityService(
        IServerConnectionStore serverStore,
        IConnectivityMonitor connectivity,
        ILogger<ServerReachabilityService> logger)
    {
        _serverStore = serverStore;
        _connectivity = connectivity;
        _logger = logger;

        // Permissive TLS for self-signed local/private hosts.
        _http = new HttpClient(OAuthHttpClientHandlerFactory.CreateHandler());
        _http.Timeout = ProbeTimeout;
    }

    /// <inheritdoc />
    public bool IsServerOnline => _isServerOnline;

    /// <inheritdoc />
    public event Action? AvailabilityChanged;

    /// <inheritdoc />
    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _cts = new CancellationTokenSource();

        // Immediately reflect device offline.
        if (!_connectivity.IsOnline)
        {
            _isServerOnline = false;
            AvailabilityChanged?.Invoke();
        }

        _ = Task.Run(() => RunAsync(_cts.Token));
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var was = _isServerOnline;

            if (!_connectivity.IsOnline)
            {
                Set(false);
            }
            else
            {
                Set(await ProbeAsync(ct).ConfigureAwait(false));
            }

            var interval = _isServerOnline ? OnlineInterval : OfflineInterval;
            try
            {
                await Task.Delay(interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<bool> ProbeAsync(CancellationToken ct)
    {
        var active = _serverStore.GetActive();
        if (active is null)
            return false;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            var url = $"{active.ServerBaseUrl.TrimEnd('/')}/health/live";
            using var response = await _http.GetAsync(url, cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Server reachability probe failed for {ServerUrl}.", active.ServerBaseUrl);
            return false;
        }
    }

    private void Set(bool online)
    {
        if (_isServerOnline == online)
            return;
        _isServerOnline = online;
        _logger.LogInformation("Server reachability changed: {State}.", online ? "online" : "offline");
        AvailabilityChanged?.Invoke();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _http.Dispose();
    }
}
