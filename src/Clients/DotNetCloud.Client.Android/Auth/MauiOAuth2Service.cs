using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace DotNetCloud.Client.Android.Auth;

/// <summary>
/// OAuth2/OIDC implementation that uses the MAUI system browser for the authorization
/// code flow with PKCE. Handles the redirect via <c>OAuthCallbackActivity</c>.
/// </summary>
internal sealed class MauiOAuth2Service : IOAuth2Service
{
    private const string RedirectUri = "net.dotnetcloud.client://oauth2redirect";
    private const string ClientId = "dotnetcloud-mobile";

    private readonly HttpClient _http = new();

    /// <inheritdoc />
    public async Task<OAuth2Result> AuthenticateAsync(string serverBaseUrl, CancellationToken ct = default)
    {
        // Build PKCE challenge
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = Guid.NewGuid().ToString("N");

        var authUrl = BuildAuthUrl(serverBaseUrl, codeChallenge, state);

        // Open system browser and wait for app callback
        var callbackUrl = await OAuthCallbackActivity.WaitForCallbackAsync(ct).ConfigureAwait(false);
        var code = ExtractCode(callbackUrl, state);

        return await ExchangeCodeAsync(serverBaseUrl, code, codeVerifier, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OAuth2Result> RefreshAsync(string serverBaseUrl, string refreshToken, CancellationToken ct = default)
    {
        var tokenEndpoint = $"{serverBaseUrl.TrimEnd('/')}/connect/token";

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = ClientId,
            ["refresh_token"] = refreshToken
        });

        using var response = await _http.PostAsync(tokenEndpoint, body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await ParseTokenResponseAsync(response, ct).ConfigureAwait(false);
    }

    // ───────────────────── helpers ─────────────────────────────────────

    private static string BuildAuthUrl(string serverBaseUrl, string codeChallenge, string state)
    {
        var baseUrl = serverBaseUrl.TrimEnd('/');
        return $"{baseUrl}/connect/authorize" +
               $"?client_id={Uri.EscapeDataString(ClientId)}" +
               $"&response_type=code" +
               $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                             $"&scope={Uri.EscapeDataString("openid profile offline_access files:read files:write")}" +
               $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
               $"&code_challenge_method=S256" +
               $"&state={state}";
    }

    private async Task<OAuth2Result> ExchangeCodeAsync(string serverBaseUrl, string code, string codeVerifier, CancellationToken ct)
    {
        var tokenEndpoint = $"{serverBaseUrl.TrimEnd('/')}/connect/token";

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["redirect_uri"] = RedirectUri,
            ["code"] = code,
            ["code_verifier"] = codeVerifier
        });

        using var response = await _http.PostAsync(tokenEndpoint, body, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return await ParseTokenResponseAsync(response, ct).ConfigureAwait(false);
    }

    private static async Task<OAuth2Result> ParseTokenResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var json = await response.Content.ReadFromJsonAsync<TokenResponse>(ct).ConfigureAwait(false)
                   ?? throw new InvalidOperationException("Empty token response.");

        return new OAuth2Result(
            json.AccessToken,
            json.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(json.ExpiresIn - 30));
    }

    private static string ExtractCode(string callbackUrl, string expectedState)
    {
        var uri = new Uri(callbackUrl);
        var query = uri.Query.TrimStart('?');
        var parts = query.Split('&');
        string? code = null;
        string? state = null;

        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            var key = Uri.UnescapeDataString(kv[0]);
            var value = Uri.UnescapeDataString(kv[1]);
            if (key == "code") code = value;
            if (key == "state") state = value;
        }

        if (state != expectedState)
            throw new InvalidOperationException("OAuth2 state mismatch — possible CSRF attack.");

        return code ?? throw new InvalidOperationException("No 'code' parameter in OAuth2 callback.");
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed class TokenResponse
    {
        public string AccessToken { get; init; } = string.Empty;
        public string RefreshToken { get; init; } = string.Empty;
        public int ExpiresIn { get; init; }
    }
}
