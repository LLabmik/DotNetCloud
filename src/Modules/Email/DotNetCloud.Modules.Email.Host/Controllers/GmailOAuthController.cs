using System.Text.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Email.Data.Services;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Modules.Email.Host.Controllers;

/// <summary>
/// Controller for Gmail OAuth 2.0 authorization flow.
///
/// Standard web-server OAuth 2.0 flow:
///   1. User clicks "Connect Gmail" in Blazor UI
///   2. Browser navigates to GET /api/v1/email/gmail/oauth/start
///   3. Server validates config, builds Google auth URL, redirects user to Google
///   4. Google authenticates user and redirects back to our callback URL
///   5. Server exchanges code for tokens, creates account, stores tokens
///   6. Server redirects user to /apps/email?gmail=connected
/// </summary>
[Route("api/v1/email/gmail/oauth")]
public class GmailOAuthController : EmailControllerBase
{
    private readonly IEmailAccountService _accountService;
    private readonly EmailCredentialEncryptionService _encryption;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GmailOAuthController> _logger;

    // Cookie name for temporarily storing OAuth state (CSRF protection).
    // Set as a short-lived cookie before redirecting to Google; verified on callback.
    private const string StateCookieName = "DotNetCloud.GmailOAuth.State";

    public GmailOAuthController(
        IEmailAccountService accountService,
        EmailCredentialEncryptionService encryption,
        IConfiguration configuration,
        ILogger<GmailOAuthController> logger)
    {
        _accountService = accountService;
        _encryption = encryption;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Checks if Gmail OAuth is configured.
    /// </summary>
    [HttpGet("status")]
    [Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore")]
    public IActionResult Status()
    {
        var configured = IsConfigured();
        return Ok(Envelope(new { configured }));
    }

    /// <summary>
    /// Starts the Gmail OAuth flow by redirecting the user's browser to Google.
    /// </summary>
    [HttpGet("start")]
    [Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore")]
    public IActionResult Start()
    {
        if (!IsConfigured(out var clientId, out var redirectUri))
        {
            _logger.LogError("Gmail OAuth is not configured (missing ClientId or RedirectUri)");
            return Redirect("/apps/email?gmail=error&reason=not_configured");
        }

        var state = Guid.NewGuid().ToString("N");
        var scope = "https://www.googleapis.com/auth/gmail.modify https://www.googleapis.com/auth/gmail.send";
        var authUrl = "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString(scope)}"
            + $"&state={state}"
            + "&access_type=offline"
            + "&prompt=consent";

        // Store state in a short-lived cookie for CSRF validation on callback
        Response.Cookies.Append(StateCookieName, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10)
        });

        _logger.LogInformation("Gmail OAuth flow started, redirecting to Google (state={State})", state);
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
            return Redirect("/apps/email?gmail=denied");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            _logger.LogError("Gmail OAuth callback missing code or state");
            return Redirect("/apps/email?gmail=error&reason=invalid_callback");
        }

        // Validate state cookie to prevent CSRF
        var expectedState = Request.Cookies[StateCookieName];
        if (string.IsNullOrWhiteSpace(expectedState) || !string.Equals(state, expectedState, StringComparison.Ordinal))
        {
            _logger.LogError("Gmail OAuth state mismatch (expected={Expected}, got={Got})", expectedState, state);
            return Redirect("/apps/email?gmail=error&reason=state_mismatch");
        }

        // Clear the state cookie
        Response.Cookies.Delete(StateCookieName);

        var caller = GetAuthenticatedCaller();

        if (!IsConfigured(out var clientId, out var clientSecret, out var redirectUri))
        {
            _logger.LogError("Gmail OAuth is not configured");
            return Redirect("/apps/email?gmail=error&reason=not_configured");
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
            return Redirect("/apps/email?gmail=error&reason=token_exchange_failed");
        }

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync();
            _logger.LogError("Gmail OAuth token exchange failed: {StatusCode} {Error}", (int)tokenResponse.StatusCode, errorBody);
            return Redirect("/apps/email?gmail=error&reason=google_rejected");
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
            return Redirect("/apps/email?gmail=error&reason=invalid_token_response");
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
        return Redirect("/apps/email?gmail=connected&account=" + account.Id.ToString("N"));
    }

    private bool IsConfigured()
    {
        var clientId = _configuration["Email:Gmail:ClientId"];
        var redirectUri = _configuration["Email:Gmail:RedirectUri"];
        return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(redirectUri);
    }

    private bool IsConfigured(out string clientId, out string redirectUri)
    {
        clientId = _configuration["Email:Gmail:ClientId"] ?? "";
        redirectUri = _configuration["Email:Gmail:RedirectUri"] ?? "";
        return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(redirectUri);
    }

    private bool IsConfigured(out string clientId, out string clientSecret, out string redirectUri)
    {
        clientId = _configuration["Email:Gmail:ClientId"] ?? "";
        clientSecret = _configuration["Email:Gmail:ClientSecret"] ?? "";
        redirectUri = _configuration["Email:Gmail:RedirectUri"] ?? "";
        return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret) && !string.IsNullOrWhiteSpace(redirectUri);
    }
}
