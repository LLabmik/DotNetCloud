using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

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
            var result = await _oauth.AuthenticateAsync(normalizedUrl, ct);

            await _tokenStore.SaveTokensAsync(normalizedUrl, result.AccessToken, result.RefreshToken, ct);

            var email = ExtractClaimFromToken(result.AccessToken, "email")
                        ?? ExtractClaimFromToken(result.AccessToken, "preferred_username")
                        ?? ExtractClaimFromToken(result.AccessToken, "name")
                        ?? new Uri(normalizedUrl).Host;
            var displayName = ExtractClaimFromToken(result.AccessToken, "name")
                              ?? new Uri(normalizedUrl).Host;
            _serverStore.Save(new ServerConnection(normalizedUrl, displayName, email));
            _serverStore.SetActive(normalizedUrl);

            _logger.LogInformation("Login succeeded for {ServerUrl}.", normalizedUrl);
            await MainThread.InvokeOnMainThreadAsync(() => LoginSucceeded?.Invoke(this, EventArgs.Empty));
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

    private static string? ExtractClaimFromToken(string accessToken, string claimName)
    {
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(claimName, out var val) ? val.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
