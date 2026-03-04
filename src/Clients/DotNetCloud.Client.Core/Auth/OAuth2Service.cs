using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using DotNetCloud.Client.Core.Api;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Core.Auth;

/// <summary>
/// OAuth2 Authorization Code + PKCE implementation that opens the system browser
/// and listens on localhost for the redirect callback.
/// </summary>
public sealed class OAuth2Service : IOAuth2Service
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int CallbackPort = 52701;
    private const string RedirectUri = "http://localhost:52701/oauth/callback";

    private readonly HttpClient _http;
    private readonly ILogger<OAuth2Service> _logger;

    /// <summary>Initializes a new <see cref="OAuth2Service"/>.</summary>
    public OAuth2Service(HttpClient httpClient, ILogger<OAuth2Service> logger)
    {
        _http = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TokenInfo> AuthorizeAsync(
        string serverBaseUrl,
        string clientId,
        IEnumerable<string> scopes,
        CancellationToken cancellationToken = default)
    {
        var (verifier, challenge) = GeneratePkce();
        var state = GenerateState();
        var scope = string.Join(" ", scopes);

        var authUrl = BuildAuthorizationUrl(serverBaseUrl, clientId, challenge, state, scope);

        _logger.LogInformation("Opening browser for OAuth2 authorization.");
        OpenBrowser(authUrl);

        var (code, returnedState) = await ListenForCallbackAsync(cancellationToken);

        if (returnedState != state)
            throw new InvalidOperationException("OAuth2 state mismatch — possible CSRF attack.");

        return await ExchangeCodeAsync(serverBaseUrl, clientId, code, verifier, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TokenInfo> RefreshAsync(
        string serverBaseUrl,
        string clientId,
        TokenInfo currentTokens,
        CancellationToken cancellationToken = default)
    {
        if (currentTokens.RefreshToken is null)
            throw new InvalidOperationException("No refresh token available.");

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = currentTokens.RefreshToken,
        };

        var tokenResponse = await PostFormAsync(serverBaseUrl, "connect/token", form, cancellationToken);
        return MapTokenResponse(tokenResponse);
    }

    /// <inheritdoc/>
    public async Task RevokeAsync(
        string serverBaseUrl,
        string clientId,
        TokenInfo tokens,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();

        if (tokens.RefreshToken is not null)
            tasks.Add(RevokeOneAsync(serverBaseUrl, clientId, tokens.RefreshToken, "refresh_token", cancellationToken));

        tasks.Add(RevokeOneAsync(serverBaseUrl, clientId, tokens.AccessToken, "access_token", cancellationToken));

        await Task.WhenAll(tasks);
    }

    // ── PKCE helpers ────────────────────────────────────────────────────────

    private static (string Verifier, string Challenge) GeneratePkce()
    {
        var verifierBytes = new byte[32];
        RandomNumberGenerator.Fill(verifierBytes);
        var verifier = Base64UrlEncode(verifierBytes);
        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Base64UrlEncode(challengeBytes);
        return (verifier, challenge);
    }

    private static string GenerateState()
    {
        var stateBytes = new byte[16];
        RandomNumberGenerator.Fill(stateBytes);
        return Base64UrlEncode(stateBytes);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    // ── Authorization URL builder ───────────────────────────────────────────

    private static string BuildAuthorizationUrl(
        string serverBaseUrl, string clientId, string challenge, string state, string scope)
    {
        var baseUrl = serverBaseUrl.TrimEnd('/');
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["response_type"] = "code";
        query["client_id"] = clientId;
        query["redirect_uri"] = RedirectUri;
        query["scope"] = scope;
        query["state"] = state;
        query["code_challenge"] = challenge;
        query["code_challenge_method"] = "S256";
        return $"{baseUrl}/connect/authorize?{query}";
    }

    // ── Localhost callback listener ─────────────────────────────────────────

    private static async Task<(string Code, string State)> ListenForCallbackAsync(CancellationToken cancellationToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{CallbackPort}/oauth/callback/");
        listener.Start();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            var contextTask = listener.GetContextAsync();
            await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, cts.Token));

            if (cts.IsCancellationRequested)
                throw new OperationCanceledException("OAuth2 authorization timed out or was cancelled.");

            var context = await contextTask;
            var query = HttpUtility.ParseQueryString(context.Request.Url?.Query ?? string.Empty);

            var code = query["code"] ?? throw new InvalidOperationException("No authorization code in callback.");
            var state = query["state"] ?? throw new InvalidOperationException("No state in callback.");
            var error = query["error"];

            if (error is not null)
                throw new InvalidOperationException($"OAuth2 authorization error: {error} — {query["error_description"]}");

            // Respond to browser with a success page
            await WriteCallbackResponseAsync(context.Response, success: true);

            return (code, state);
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task WriteCallbackResponseAsync(HttpListenerResponse response, bool success)
    {
        var html = success
            ? "<html><body><h1>Authorization successful!</h1><p>You may close this window.</p></body></html>"
            : "<html><body><h1>Authorization failed.</h1><p>Please try again.</p></body></html>";

        var bytes = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }

    // ── Token exchange ──────────────────────────────────────────────────────

    private async Task<TokenInfo> ExchangeCodeAsync(
        string serverBaseUrl, string clientId, string code, string verifier, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["code_verifier"] = verifier,
        };

        var tokenResponse = await PostFormAsync(serverBaseUrl, "connect/token", form, cancellationToken);
        return MapTokenResponse(tokenResponse);
    }

    private async Task RevokeOneAsync(
        string serverBaseUrl, string clientId, string token, string tokenTypeHint, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["token"] = token,
            ["token_type_hint"] = tokenTypeHint,
        };
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await _http.PostAsync($"{serverBaseUrl.TrimEnd('/')}/connect/revocation", content, cancellationToken);
            // 200 OK expected; ignore other status codes for revocation
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token revocation failed for {TokenTypeHint}.", tokenTypeHint);
        }
    }

    private async Task<TokenResponse> PostFormAsync(
        string serverBaseUrl, string path, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await _http.PostAsync($"{serverBaseUrl.TrimEnd('/')}/{path}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Empty token response.");
    }

    private static TokenInfo MapTokenResponse(TokenResponse r) =>
        new()
        {
            AccessToken = r.AccessToken,
            RefreshToken = r.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(r.ExpiresIn > 0 ? r.ExpiresIn - 30 : 3570),
        };

    // ── Browser launch ──────────────────────────────────────────────────────

    private static void OpenBrowser(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Best-effort: some environments cannot launch a browser
        }
    }
}
