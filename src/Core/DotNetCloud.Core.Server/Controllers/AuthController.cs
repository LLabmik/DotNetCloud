using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Authentication endpoints for user registration, login, logout, password management, and token refresh.
/// </summary>
[ApiController]
[Route("api/v1/core/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthController"/> class.
    /// </summary>
    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ---------------------------------------------------------------------------
    // User Authentication
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Register a new user account.
    /// </summary>
    /// <param name="request">Registration request containing email, password, and profile information.</param>
    /// <returns>Registration result with the new user's ID.</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request)
    {
        try
        {
            var caller = BuildCallerContext();
            var response = await _authService.RegisterAsync(request, caller);
            _logger.LogInformation("User registered: {Email} ({UserId})", request.Email, response.UserId);
            return Ok(new { success = true, data = response });
        }
        catch (Errors.ValidationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed: {Message}", ex.Message);
            return BadRequest(new { success = false, error = new { code = "REGISTRATION_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Log in a user with email and password.
    /// </summary>
    /// <param name="request">Login request containing email and password.</param>
    /// <returns>Login response with user info or MFA requirement.</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        try
        {
            var caller = BuildCallerContext();
            var response = await _authService.LoginAsync(request, caller);
            _logger.LogInformation("User logged in: {Email}", request.Email);
            return Ok(new { success = true, data = response });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_CREDENTIALS", message = "Invalid email or password" } });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("MFA"))
        {
            _logger.LogInformation("MFA required for {Email}", request.Email);
            return Accepted(new { success = false, error = new { code = "MFA_REQUIRED", message = "Multi-factor authentication required" } });
        }
    }

    /// <summary>
    /// Log out the current user and revoke their tokens.
    /// </summary>
    /// <returns>Confirmation that the user was logged out.</returns>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> LogoutAsync()
    {
        var userIdClaim = User.FindFirst("sub")?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            try
            {
                var caller = BuildCallerContext();
                await _authService.LogoutAsync(userId, null, caller);
                _logger.LogInformation("User logged out: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout error for user {UserId}", userId);
            }
        }

        return Ok(new { success = true, message = "Logged out successfully." });
    }

    /// <summary>
    /// Refresh an access token using a refresh token.
    /// </summary>
    /// <param name="request">Refresh token request.</param>
    /// <returns>New token pair.</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var caller = BuildCallerContext();
            var response = await _authService.RefreshTokenAsync(request, caller);
            return Ok(new { success = true, data = response });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_REFRESH_TOKEN", message = "Invalid or expired refresh token" } });
        }
    }

    /// <summary>
    /// Get the current authenticated user's profile information.
    /// </summary>
    /// <returns>User profile information.</returns>
    [HttpGet("user")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUserAsync()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var profile = await _authService.GetUserProfileAsync(userId);
        if (profile is null)
        {
            return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found" } });
        }

        return Ok(new { success = true, data = profile });
    }

    // ---------------------------------------------------------------------------
    // OAuth2/OIDC Integration
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Initiate external provider authentication (e.g., Google, GitHub).
    /// </summary>
    /// <param name="provider">The external authentication provider name.</param>
    /// <param name="returnUrl">URL to redirect to after authentication.</param>
    /// <returns>A challenge result that redirects to the external provider.</returns>
    [HttpGet("external-login/{provider}")]
    [AllowAnonymous]
    public IActionResult ExternalLogin(string provider, [FromQuery] string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalCallback), "Auth", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, provider);
    }

    /// <summary>
    /// Handle the callback from an external authentication provider.
    /// </summary>
    /// <param name="returnUrl">URL to redirect to after processing.</param>
    /// <returns>Redirect to the return URL or an error response.</returns>
    [HttpGet("external-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalCallback([FromQuery] string? returnUrl = null)
    {
        // External login callback processing will be wired to Identity's ExternalLoginSignInAsync
        // when external providers are configured. For now, return a placeholder.
        var info = await HttpContext.AuthenticateAsync();
        if (!info.Succeeded)
        {
            return Unauthorized(new { success = false, error = new { code = "EXTERNAL_LOGIN_FAILED", message = "External authentication failed" } });
        }

        _logger.LogInformation("External login callback received");
        return Ok(new { success = true, message = "External authentication successful", returnUrl });
    }

    // ---------------------------------------------------------------------------
    // Password Management
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Change the current user's password.
    /// </summary>
    /// <param name="request">Request with current and new passwords.</param>
    /// <returns>Confirmation that the password was changed.</returns>
    [HttpPost("password/change")]
    [Authorize]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var success = await _authService.ChangePasswordAsync(userId, request);
        if (!success)
        {
            return BadRequest(new { success = false, error = new { code = "PASSWORD_CHANGE_FAILED", message = "Current password is incorrect or new password does not meet requirements." } });
        }

        _logger.LogInformation("Password changed for user {UserId}", userId);
        return Ok(new { success = true, message = "Password changed successfully." });
    }

    /// <summary>
    /// Request a password reset email.
    /// </summary>
    /// <param name="request">Request containing the user's email address.</param>
    /// <returns>Confirmation (always 200 to prevent email enumeration).</returns>
    [HttpPost("password/forgot")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPasswordAsync([FromBody] PasswordResetRequestDto request)
    {
        // Always return success to prevent email enumeration
        try
        {
            await _authService.InitiatePasswordResetAsync(request.Email);
            _logger.LogInformation("Password reset requested for {Email}", request.Email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Password reset initiation error for {Email} (suppressed)", request.Email);
        }

        return Ok(new { success = true, message = "If the account exists, a password reset email has been sent." });
    }

    /// <summary>
    /// Reset a user's password using a reset token.
    /// </summary>
    /// <param name="request">Reset request with email, token, and new password.</param>
    /// <returns>Whether the password was reset successfully.</returns>
    [HttpPost("password/reset")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordRequest request)
    {
        var success = await _authService.ResetPasswordAsync(request);

        if (!success)
        {
            return BadRequest(new { success = false, error = new { code = "RESET_FAILED", message = "Invalid or expired reset token." } });
        }

        _logger.LogInformation("Password reset for {Email}", request.Email);
        return Ok(new { success = true, message = "Password has been reset successfully." });
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out userId);
    }

    private CallerContext BuildCallerContext()
    {
        var userIdClaim = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
        {
            return CallerContext.CreateSystemContext();
        }

        var roles = User.FindAll("role").Select(c => c.Value).ToList();
        return new CallerContext(userId, roles, CallerType.User);
    }
}

/// <summary>
/// Request DTO for initiating a password reset (contains only email).
/// </summary>
public sealed class PasswordResetRequestDto
{
    /// <summary>
    /// Gets or sets the email address to send the reset link to.
    /// </summary>
    public string Email { get; set; } = string.Empty;
}
