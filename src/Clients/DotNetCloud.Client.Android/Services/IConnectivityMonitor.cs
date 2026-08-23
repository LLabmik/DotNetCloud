namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Observes the device's network connectivity and raises <see cref="ConnectivityRestored"/>
/// when the device transitions from offline to online so queued operations can be flushed.
/// </summary>
public interface IConnectivityMonitor
{
    /// <summary>Whether the device currently has internet access.</summary>
    bool IsOnline { get; }

    /// <summary>Raised once when the device regains internet access after being offline.</summary>
    event Action? ConnectivityRestored;

    /// <summary>Begins monitoring connectivity. Safe to call multiple times.</summary>
    void Start();
}
