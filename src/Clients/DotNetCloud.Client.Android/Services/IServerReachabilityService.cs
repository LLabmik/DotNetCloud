namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Tracks whether the configured DotNetCloud server is reachable, distinct from
/// device internet connectivity (a phone can have internet while the server is down).
/// </summary>
public interface IServerReachabilityService
{
    /// <summary>Whether the active server responded to a liveness ping recently.</summary>
    bool IsServerOnline { get; }

    /// <summary>Raised whenever online status transitions.</summary>
    event Action? AvailabilityChanged;

    /// <summary>Starts periodic probing. Safe to call multiple times.</summary>
    void Start();
}
