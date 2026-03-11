using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>ViewModel for the login / server-setup screen.</summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IOAuth2Service _oauth;
    private readonly ISecureTokenStore _tokenStore;
    private readonly IServerConnectionStore _serverStore;
    private readonly ILogger<LoginViewModel> _logger;

    /// <summary>Raised when login succeeds and the app should navigate to the channel list.</summary>
    public event EventHandler? LoginSucceeded;

    /// <summary>Initializes a new <see cref="LoginViewModel"/>.</summary>
    public LoginViewModel(
        IOAuth2Service oauth,
        ISecureTokenStore tokenStore,
        IServerConnectionStore serverStore,
        ILogger<LoginViewModel> logger)
    {
        _oauth = oauth;
        _tokenStore = tokenStore;
        _serverStore = serverStore;
        _logger = logger;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _serverUrl = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Initiates the OAuth2 login flow for the entered server URL.</summary>
    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync(CancellationToken ct)
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            var normalizedUrl = NormalizeUrl(ServerUrl);
            var result = await _oauth.AuthenticateAsync(normalizedUrl, ct).ConfigureAwait(false);

            await _tokenStore.SaveTokensAsync(normalizedUrl, result.AccessToken, result.RefreshToken, ct).ConfigureAwait(false);
            _serverStore.SetActive(normalizedUrl);

            _logger.LogInformation("Login succeeded for {ServerUrl}.", normalizedUrl);
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Login cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for {ServerUrl}.", ServerUrl);
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLogin() => !string.IsNullOrWhiteSpace(ServerUrl) && !IsBusy;

    private static string NormalizeUrl(string url)
    {
        url = url.Trim();
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        return url.TrimEnd('/');
    }
}
