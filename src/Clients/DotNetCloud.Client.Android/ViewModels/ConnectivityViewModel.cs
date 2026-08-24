using CommunityToolkit.Mvvm.ComponentModel;
using DotNetCloud.Client.Android.Services;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// Drives the global "server offline" banner.
/// </summary>
public partial class ConnectivityViewModel : ObservableObject
{
    private readonly IServerReachabilityService _reachability;

    /// <summary>Whether the active server is currently unreachable.</summary>
    [ObservableProperty]
    private bool _isServerOffline;

    /// <summary>Creates a new view model bound to the reachability service.</summary>
    public ConnectivityViewModel(IServerReachabilityService reachability)
    {
        _reachability = reachability;
        IsServerOffline = !reachability.IsServerOnline;
        _reachability.AvailabilityChanged += OnAvailabilityChanged;
    }

    private void OnAvailabilityChanged() =>
        IsServerOffline = !_reachability.IsServerOnline;
}
