using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Email.Data.Services;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Email.Host.Controllers;

/// <summary>
/// Controller for Gmail OAuth 2.0 authorization flow.
///
/// Standard web-server OAuth 2.0 flow, adapted to the sandboxed module UI plane:
///   1. The module UI opens GET /api/v1/email/gmail/oauth/start in a core-controlled tab
///   2. Server validates config, builds Google auth URL, redirects the tab to Google
///   3. Google authenticates the user and redirects back to our callback URL
///   4. Server exchanges the code for tokens, creates the account, stores tokens
///   5. Server redirects the tab to /apps/dotnetcloud.email/?gmail=connected
///
/// CSRF protection uses a DataProtection-signed <c>state</c> value rather than a cookie: the core
/// module proxy strips <c>Set-Cookie</c> from module responses, and the framed document has an opaque
/// origin, so a module cookie could never round-trip. The state binds the flow to the user the core
/// asserted and expires after ten minutes.
/// </summary>
[Route("api/v1/email/gmail/oauth")]
public class GmailOAuthController : EmailControllerBase
{
    /// <summary>The module id this host runs as; prefix of every UI-plane URL.</summary>
    private const string ModuleId = "dotnetcloud.email";

    /// <summary>DataProtection purpose for the signed OAuth state value.</summary>
    private const string StateProtectionPurpose = "DotNetCloud.Modules.Email.GmailOAuth.State";

    /// <summary>How long a started authorization flow may stay outstanding.</summary>
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private readonly IEmailAccountService _accountService;
    private readonly EmailCredentialEncryptionService _encryption;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GmailOAuthController> _logger;
    private readonly IDataProtector _stateProtector;

    public GmailOAuthController(
        IEmailAccountService accountService,
        EmailCredentialEncryptionService encryption,
        IConfiguration configuration,
        ILogger<GmailOAuthController> logger,
        IDataProtectionProvider dataProtectionProvider)
    {
        _accountService = accountService;
        _encryption = encryption;
        _configuration = configuration;
        _logger = logger;
        _stateProtector = dataProtectionProvider.CreateProtector(StateProtectionPurpose);
    }

    /// <summary>
    /// Checks if Gmail OAuth is configured.
    /// </summary>
    [HttpGet("status")]
    public IActionResult Status()
    {
        var configured = IsConfigured();
        return Ok(Envelope(new { configured }));
    }

    /// <summary>
    /// Starts the Gmail OAuth flow by redirecting the user's browser to Google.
    /// </summary>
    [HttpGet("start")]
    public IActionResult Start()
    {
        CallerContext caller;
        try
        {
            caller = GetAuthenticatedCaller();
        }
        catch (UnauthorizedAccessException)
        {
            return Redirect(ModuleUrl("gmail=error&reason=not_signed_in"));
        }

        if (!IsConfigured(out var clientId, out var redirectUri))
        {
            _logger.LogError("Gmail OAuth is not configured (missing Email:Gmail:ClientId or a redirect URI)");
            return Redirect(ModuleUrl("gmail=error&reason=not_configured"));
        }

        var state = CreateState(caller.UserId);
        var scope = "https://www.googleapis.com/auth/gmail.modify https://www.googleapis.com/auth/gmail.send";
        var authUrl = "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString(scope)}"
            + $"&state={Uri.EscapeDataString(state)}"
            + "&access_type=offline"
            + "&prompt=consent";

        _logger.LogInformation("Gmail OAuth flow started, redirecting to Google");

        // Validate the redirect URL is a known Google OAuth endpoint to prevent open redirect.
        const string expectedPrefix = "https://accounts.google.com/o/oauth2/v2/auth?";
        if (!authUrl.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Gmail OAuth auth URL validation failed");
            return Redirect(ModuleUrl("gmail=error&reason=invalid_auth_url"));
        }

        return Redirect(authUrl);
    }

    /// <summary>
    /// Handles the callback from Google after the user authorizes the app.
    /// Exchanges the authorization code for tokens and creates the account.
    /// </summary>
    [HttpGet("complete")]
    public async Task<IActionResult> Complete([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        // If user denied access, Google sends ?error=access_denied
        if (!string.IsNullOrWhiteSpace(error))
        {
            _logger.LogWarning("Gmail OAuth denied by user (error={Error})", error);
            return Redirect(ModuleUrl("gmail=denied"));
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            _logger.LogError("Gmail OAuth callback missing code or state");
            return Redirect(ModuleUrl("gmail=error&reason=invalid_callback"));
        }

        CallerContext caller;
        try
        {
            caller = GetAuthenticatedCaller();
        }
        catch (UnauthorizedAccessException)
        {
            return Redirect(ModuleUrl("gmail=error&reason=not_signed_in"));
        }

        // The signed state replaces the cookie the core proxy would have stripped, and binds the flow
        // to the user the core asserted for this request.
        if (!TryReadState(state, caller.UserId))
        {
            _logger.LogError("Gmail OAuth state validation failed for the asserted user");
            return Redirect(ModuleUrl("gmail=error&reason=state_mismatch"));
        }

        if (!IsConfigured(out var clientId, out var clientSecret, out var redirectUri))
        {
            _logger.LogError("Gmail OAuth is not configured");
            return Redirect(ModuleUrl("gmail=error&reason=not_configured"));
        }

        // Exchange authorization code for tokens
        using var httpClient = new HttpClient();
        var tokenParams = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        };

        HttpResponseMessage tokenResponse;
        try
        {
            tokenResponse = await httpClient.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(tokenParams));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange Gmail OAuth code for tokens");
            return Redirect(ModuleUrl("gmail=error&reason=token_exchange_failed"));
        }

        if (!tokenResponse.IsSuccessStatusCode)
        {
            // Log only the status code, not the full error body (may contain sensitive OAuth details)
            _logger.LogError("Gmail OAuth token exchange failed: HTTP {StatusCode}", (int)tokenResponse.StatusCode);
            return Redirect(ModuleUrl("gmail=error&reason=google_rejected"));
        }

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        using var tokenDoc = JsonDocument.Parse(tokenJson);
        var root = tokenDoc.RootElement;

        var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;
        var expiresIn = root.TryGetProperty("expires_in", out var ei) ? ei.GetInt64() : 3600;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogError("Gmail OAuth response missing access_token");
            return Redirect(ModuleUrl("gmail=error&reason=invalid_token_response"));
        }

        // Fetch user profile to get email address
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var profileResponse = await httpClient.GetAsync("https://www.googleapis.com/gmail/v1/users/me/profile");
        string emailAddress;

        if (profileResponse.IsSuccessStatusCode)
        {
            var profileJson = await profileResponse.Content.ReadAsStringAsync();
            using var profileDoc = JsonDocument.Parse(profileJson);
            emailAddress = profileDoc.RootElement.TryGetProperty("emailAddress", out var ea) ? ea.GetString() ?? "" : "";
        }
        else
        {
            emailAddress = "";
        }

        var token = new
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn)
        };
        var tokenBlob = JsonSerializer.Serialize(token);
        var credentialBytes = System.Text.Encoding.UTF8.GetBytes(tokenBlob);

        var encryptedBlob = _encryption.Protect(credentialBytes, caller.UserId);

        var account = await _accountService.CreateAsync(new CreateEmailAccountRequest
        {
            ProviderType = EmailProviderType.Gmail,
            DisplayName = emailAddress,
            EmailAddress = emailAddress,
            CredentialsJson = encryptedBlob
        }, caller);

        _logger.LogInformation("Gmail OAuth flow completed for user {UserId}, account {AccountId}", caller.UserId, account.Id);
        return Redirect(ModuleUrl("gmail=connected&account=" + account.Id.ToString("N")));
    }

    /// <summary>Builds the module app URL the browser returns to after the flow.</summary>
    private static string ModuleUrl(string query) => $"/apps/{ModuleId}/?{query}";

    /// <summary>
    /// Signs a state value binding the flow to <paramref name="userId"/> and to a short expiry.
    /// </summary>
    private string CreateState(Guid userId)
    {
        var payload = $"{userId:N}|{DateTimeOffset.UtcNow.Add(StateLifetime).ToUnixTimeSeconds()}";
        return _stateProtector.Protect(payload);
    }

    /// <summary>
    /// Verifies a state value produced by <see cref="CreateState"/> for the asserted user.
    /// </summary>
    /// <returns><see langword="true"/> when the state is authentic, unexpired and for this user.</returns>
    private bool TryReadState(string state, Guid expectedUserId)
    {
        string payload;
        try
        {
            payload = _stateProtector.Unprotect(state);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }

        var parts = payload.Split('|');
        if (parts.Length != 2
            || !Guid.TryParseExact(parts[0], "N", out var userId)
            || !long.TryParse(parts[1], out var expiresUnix))
        {
            return false;
        }

        return userId == expectedUserId
            && DateTimeOffset.UtcNow.ToUnixTimeSeconds() <= expiresUnix;
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(GetClientId());
    }

    private bool IsConfigured(out string clientId, out string redirectUri)
    {
        clientId = GetClientId();
        redirectUri = GetRedirectUri();
        return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(redirectUri);
    }

    private bool IsConfigured(out string clientId, out string clientSecret, out string redirectUri)
    {
        clientId = GetClientId();
        clientSecret = GetClientSecret();
        redirectUri = GetRedirectUri();
        return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret) && !string.IsNullOrWhiteSpace(redirectUri);
    }

    /// <summary>
    /// Gets the Gmail OAuth client ID.
    ///
    /// The DotNetCloud project registers a single Google Cloud OAuth 2.0 client
    /// (Web application type) that all installations share — like Thunderbird does
    /// with Mozilla's client. The built-in default is that shared client ID.
    ///
    /// Server admins who want to use their own Google Cloud project can override
    /// via appsettings.json: "Email:Gmail:ClientId".
    /// </summary>
    /// <remarks>
    /// An unconfigured deployment is a normal state, not an error: the UI hides the Gmail option.
    /// Returning an empty value keeps <c>/gmail/oauth/status</c> answerable instead of throwing.
    /// </remarks>
    private string GetClientId()
    {
        return _configuration["Email:Gmail:ClientId"] ?? string.Empty;
    }

    /// <summary>
    /// Gets the Gmail OAuth client secret. See <see cref="GetClientId"/>.
    /// </summary>
    private string GetClientSecret()
    {
        return _configuration["Email:Gmail:ClientSecret"] ?? string.Empty;
    }

    /// <summary>
    /// Gets the redirect URI for Gmail OAuth callbacks. Auto-detected from the current request's
    /// public origin plus the module app prefix, or overridden via appsettings.json.
    /// </summary>
    /// <remarks>
    /// The core proxy strips <c>/apps/dotnetcloud.email</c> before forwarding, so the callback has to
    /// carry that prefix: Google redirects the browser to the public address, which is the proxied
    /// one. <c>X-Forwarded-Host</c>/<c>X-Forwarded-Proto</c> are honored (see Program.cs).
    /// </remarks>
    private string GetRedirectUri()
    {
        var configured = _configuration["Email:Gmail:RedirectUri"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host.Value}/apps/{ModuleId}/api/v1/email/gmail/oauth/complete";
    }
}
