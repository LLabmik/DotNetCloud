using DotNetCloud.Core.Auth;
using DotNetCloud.Core.Dtos.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Authentication endpoints for user registration, login, password reset, and token refresh.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
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

    /// <summary>
    /// Register a new user account.
    /// </summary>
    /// <param name="request">Registration request containing email and password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Registration response with user ID and confirmation status</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _authService.RegisterAsync(request, cancellationToken);
            _logger.LogInformation("User registered successfully: {UserId}", response.UserId);
            return CreatedAtAction(nameof(GetProfileAsync), new { }, response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Registration validation failed: {Message}", ex.Message);
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
    /// <param name="request">Login request containing email and password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Login response with access token or MFA requirement</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _authService.LoginAsync(request, cancellationToken);
            _logger.LogInformation("User logged in successfully: {Email}", request.Email);
            return Ok(new { success = true, data = response });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed for email {Email}: {Message}", request.Email, ex.Message);
            return Unauthorized(new { success = false, error = new { code = "INVALID_CREDENTIALS", message = "Invalid email or password" } });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("MFA"))
        {
            _logger.LogInformation("MFA required for email {Email}", request.Email);
            return Accepted(new { success = false, error = new { code = "MFA_REQUIRED", message = "Multi-factor authentication required" } });
        }
    }

    /// <summary>
    /// Refresh an access token using a refresh token.
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New access token response</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(request.RefreshToken, cancellationToken);
            return Ok(new { success = true, data = response });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_REFRESH_TOKEN", message = "Invalid or expired refresh token" } });
        }
    }

    /// <summary>
    /// Request a password reset for an account.
    /// </summary>
    /// <param name="request">Password reset request with email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Confirmation that password reset email was sent</returns>
    [HttpPost("password-reset-request")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestPasswordResetAsync([FromBody] PasswordResetRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            await _authService.InitiatePasswordResetAsync(request.Email, cancellationToken);
            _logger.LogInformation("Password reset requested for email {Email}", request.Email);
            return Ok(new { success = true, message = "Password reset email sent" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Password reset request failed for email {Email}: {Message}", request.Email, ex.Message);
            // Don't reveal if email exists (security best practice)
            return Ok(new { success = true, message = "Password reset email sent" });
        }
    }

    /// <summary>
    /// Reset a user's password using a reset token.
    /// </summary>
    /// <param name="request">Password reset request with token and new password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Confirmation that password was reset</returns>
    [HttpPost("password-reset")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] PasswordResetDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            await _authService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, cancellationToken);
            _logger.LogInformation("Password reset completed for email {Email}", request.Email);
            return Ok(new { success = true, message = "Password has been reset" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "RESET_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Get the current user's profile.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current user's profile information</returns>
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
            }

            var profile = await _authService.GetUserProfileAsync(userId, cancellationToken);
            return Ok(new { success = true, data = profile });
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found" } });
        }
    }

    /// <summary>
    /// Update the current user's profile.
    /// </summary>
    /// <param name="request">Update profile request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated profile information</returns>
    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfileAsync([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
            }

            var profile = await _authService.UpdateUserProfileAsync(userId, request, cancellationToken);
            _logger.LogInformation("Profile updated for user {UserId}", userId);
            return Ok(new { success = true, data = profile });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found" } });
        }
    }

    /// <summary>
    /// Change the current user's password.
    /// </summary>
    /// <param name="request">Change password request with current and new passwords</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Confirmation that password was changed</returns>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
            }

            await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
            _logger.LogInformation("Password changed for user {UserId}", userId);
            return Ok(new { success = true, message = "Password changed successfully" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "VALIDATION_ERROR", message = ex.Message } });
        }
        catch (UnauthorizedAccessException)
        {
            return BadRequest(new { success = false, error = new { code = "INVALID_PASSWORD", message = "Current password is incorrect" } });
        }
    }

    /// <summary>
    /// Log out the current user and revoke their tokens.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Confirmation that user was logged out</returns>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
            {
                await _authService.LogoutAsync(userId, cancellationToken);
                _logger.LogInformation("User logged out: {UserId}", userId);
            }

            return Ok(new { success = true, message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout failed");
            return Ok(new { success = true, message = "Logged out successfully" });
        }
    }
}
