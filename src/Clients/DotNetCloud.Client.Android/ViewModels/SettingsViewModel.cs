using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>ViewModel for the settings and linked accounts screen.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IServerConnectionStore _serverStore;
    private readonly ISecureTokenStore _tokenStore;
    private readonly ILogger<SettingsViewModel> _logger;

    /// <summary>Raised when the user logs out and the app should return to login.</summary>
    public event EventHandler? LoggedOut;

    /// <summary>Initializes a new <see cref="SettingsViewModel"/>.</summary>
    public SettingsViewModel(
        IServerConnectionStore serverStore,
        ISecureTokenStore tokenStore,
        ILogger<SettingsViewModel> logger)
    {
        _serverStore = serverStore;
        _tokenStore = tokenStore;
        _logger = logger;

        var active = serverStore.GetActive();
        ServerDisplayName = active?.DisplayName ?? string.Empty;
        AccountEmail = active?.AccountEmail ?? string.Empty;
        ServerBaseUrl = active?.ServerBaseUrl ?? string.Empty;
    }

    /// <summary>Display name of the connected server.</summary>
    [ObservableProperty]
    private string _serverDisplayName = string.Empty;

    /// <summary>Email of the logged-in account.</summary>
    [ObservableProperty]
    private string _accountEmail = string.Empty;

    /// <summary>Base URL of the active server connection.</summary>
    [ObservableProperty]
    private string _serverBaseUrl = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Logs out from the current server, clears stored tokens, and raises <see cref="LoggedOut"/>.</summary>
    [RelayCommand]
    private async Task LogOutAsync(CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            if (!string.IsNullOrEmpty(ServerBaseUrl))
            {
                await _tokenStore.DeleteTokensAsync(ServerBaseUrl, ct);
                _serverStore.Remove(ServerBaseUrl);
            }
            _logger.LogInformation("User logged out from {ServerBaseUrl}.", ServerBaseUrl);
            await MainThread.InvokeOnMainThreadAsync(() => LoggedOut?.Invoke(this, EventArgs.Empty));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout failed.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
