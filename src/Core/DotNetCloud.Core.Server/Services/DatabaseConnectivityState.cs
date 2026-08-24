namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Holds the cached database availability determined by the background
/// <see cref="DatabaseReconnectMonitor"/>. Consumers (health check, middleware)
/// read this instead of probing the DB on every request.
/// </summary>
public sealed class DatabaseConnectivityState
{
    private volatile bool _isAvailable = true;

    /// <summary>Whether the database is currently reachable.</summary>
    public bool IsAvailable => _isAvailable;

    /// <summary>Raised when availability transitions between up and down.</summary>
    public event EventHandler? AvailabilityChanged;

    /// <summary>Updates availability and raises <see cref="AvailabilityChanged"/> on change.</summary>
    public void SetAvailable(bool available)
    {
        var previous = _isAvailable;
        _isAvailable = available;
        if (previous != available)
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }
}
