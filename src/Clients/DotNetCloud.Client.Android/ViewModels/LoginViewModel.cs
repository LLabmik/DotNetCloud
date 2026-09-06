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
    private string _serverUrl = LoadLastServerUrl();

    /// <summary>
    /// Loads the most recently used server URL from stored connections.
    /// Checks the active connection first, then falls back to the last saved entry.
    /// Returns empty string if no saved servers exist.
    /// </summary>
    private static string LoadLastServerUrl()
    {
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is null)
                return string.Empty;
            var store = services.GetService(typeof(IServerConnectionStore)) as IServerConnectionStore;
            if (store is null)
                return string.Empty;

            var active = store.GetActive();
            if (active is not null)
                return active.ServerBaseUrl;

            var all = store.GetAll();
            if (all.Count > 0)
                return all[^1].ServerBaseUrl;

            // Fall back to URL saved during logout
            var prefs = services.GetService(typeof(IAppPreferences)) as IAppPreferences;
            if (prefs is not null)
            {
                var saved = prefs.Get<string>("last_server_url", string.Empty);
                if (!string.IsNullOrEmpty(saved))
                    return saved;
            }
        }
        catch
        {
            // Best-effort — if resolution fails, start with blank field
        }
        return string.Empty;
    }

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

            await _tokenStore.SaveTokensAsync(normalizedUrl, result.AccessToken, result.RefreshToken, result.IdToken, result.ExpiresAt, ct);

            // Extract user info from the id_token (signed JWT, decodable client-side).
            // The access token is JWE-encrypted so we cannot decode it here.
            // preferred_username is authoritative (email may be absent for no-email accounts).
            var email = ExtractClaimFromToken(result.IdToken ?? result.AccessToken, "preferred_username")
                        ?? ExtractClaimFromToken(result.IdToken ?? result.AccessToken, "email")
                        ?? ExtractClaimFromToken(result.IdToken ?? result.AccessToken, "name")
                        ?? new Uri(normalizedUrl).Host;
            var displayName = ExtractClaimFromToken(result.IdToken ?? result.AccessToken, "name")
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

    /// <summary>Returns the active server URL if a saved connection exists, else null.
    /// Used by <see cref="Views.LoginPage.OnAppearing"/> to skip login on warm start.</summary>
    internal string? TryGetActiveConnection() => _serverStore.GetActive()?.ServerBaseUrl;

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
            if (parts.Length < 2)
                return null;

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
