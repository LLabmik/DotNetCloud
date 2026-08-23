using Microsoft.Extensions.Logging;
using Microsoft.Maui.Networking;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// <see cref="IConnectivityMonitor"/> implementation backed by MAUI's
/// <see cref="Connectivity"/> API. Fires <see cref="IConnectivityMonitor.ConnectivityRestored"/>
/// once per offline→online transition (debounced briefly to avoid flapping).
/// </summary>
internal sealed class ConnectivityMonitorService : IConnectivityMonitor, IDisposable
{
    private readonly ILogger<ConnectivityMonitorService> _logger;
    private bool _wasOnline;
    private bool _started;

    /// <summary>Initializes a new <see cref="ConnectivityMonitorService"/>.</summary>
    public ConnectivityMonitorService(ILogger<ConnectivityMonitorService> logger)
    {
        _logger = logger;
        _wasOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
    }

    /// <inheritdoc />
    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    /// <inheritdoc />
    public event Action? ConnectivityRestored;

    /// <inheritdoc />
    public void Start()
    {
        if (_started)
            return;
        _started = true;
        _wasOnline = IsOnline;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        _logger.LogDebug("ConnectivityMonitor started (currently {State}).", IsOnline ? "online" : "offline");
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var online = e.NetworkAccess == NetworkAccess.Internet;
        _logger.LogDebug("Connectivity changed: {State}.", online ? "online" : "offline");

        if (online && !_wasOnline)
        {
            // Delay slightly so transient reconnects stabilize before flushing.
            _ = Task.Run(async () =>
            {
                await Task.Delay(1500).ConfigureAwait(false);
                if (IsOnline)
                    ConnectivityRestored?.Invoke();
            });
        }

        _wasOnline = online;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }
}
