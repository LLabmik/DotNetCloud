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
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = "Identity.Application,OpenIddict.Validation.AspNetCore")]
[Route("api/v1/email/gmail/oauth")]
public class GmailOAuthController : EmailControllerBase
{
    private readonly IEmailAccountService _accountService;
    private readonly EmailCredentialEncryptionService _encryption;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GmailOAuthController> _logger;

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
    public IActionResult Status()
    {
        var clientId = _configuration["Email:Gmail:ClientId"];
        var redirectUri = _configuration["Email:Gmail:RedirectUri"];
        var configured = !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(redirectUri);
        return Ok(Envelope(new { configured }));
    }

    /// <summary>
    /// Starts the Gmail OAuth flow. Returns the authorization URL and state token.
    /// </summary>
    [HttpPost("start")]
    public IActionResult Start()
    {
        var clientId = _configuration["Email:Gmail:ClientId"];
        var redirectUri = _configuration["Email:Gmail:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
        {
            _logger.LogError("Gmail OAuth is not configured (missing ClientId or RedirectUri)");
            return BadRequest(ErrorEnvelope(ErrorCodes.EmailGmailOAuthFailed, "Gmail OAuth is not configured."));
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

        _logger.LogInformation("Gmail OAuth flow started (state={State})", state);
        return Ok(new { authUrl, state });
    }

    /// <summary>
    /// Completes the Gmail OAuth flow. Exchanges the authorization code for tokens and creates the account.
    /// </summary>
    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] GmailOAuthCompleteRequest request)
    {
        var caller = GetAuthenticatedCaller();
        var clientId = _configuration["Email:Gmail:ClientId"];
        var clientSecret = _configuration["Email:Gmail:ClientSecret"];
        var redirectUri = _configuration["Email:Gmail:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri))
        {
            _logger.LogError("Gmail OAuth is not configured");
            return StatusCode(500, ErrorEnvelope(ErrorCodes.EmailGmailOAuthFailed, "Gmail OAuth is not configured."));
        }

        // Exchange authorization code for tokens
        using var httpClient = new HttpClient();
        var tokenParams = new Dictionary<string, string>
        {
            ["code"] = request.Code,
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
            return StatusCode(502, ErrorEnvelope(ErrorCodes.EmailGmailOAuthFailed, "Failed to contact Google OAuth endpoint."));
        }

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync();
            _logger.LogError("Gmail OAuth token exchange failed: {StatusCode} {Error}", (int)tokenResponse.StatusCode, errorBody);
            return StatusCode(502, ErrorEnvelope(ErrorCodes.EmailGmailOAuthFailed, "Google rejected the authorization code."));
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
            return StatusCode(502, ErrorEnvelope(ErrorCodes.EmailGmailOAuthFailed, "Invalid token response from Google."));
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
            CredentialsJson = tokenBlob
        }, caller);

        _logger.LogInformation("Gmail OAuth flow completed for user {UserId}, account {AccountId}", caller.UserId, account.Id);
        return Ok(new { account.Id, account.DisplayName, account.EmailAddress });
    }
}

/// <summary>Request DTO for completing Gmail OAuth.</summary>
public sealed record GmailOAuthCompleteRequest
{
    /// <summary>The authorization code from Google.</summary>
    public required string Code { get; init; }

    /// <summary>The state token returned from the start endpoint.</summary>
    public required string State { get; init; }
}
