using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace DotNetCloud.Core.Auth.Services;

/// <summary>
/// Implements <see cref="IAuthService"/> using ASP.NET Core Identity and OpenIddict.
/// </summary>
/// <remarks>
/// This service handles credential validation and user lifecycle. Actual JWT token
/// issuance occurs at the HTTP endpoint layer via OpenIddict's token endpoint (Phase 0.7).
/// </remarks>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IOpenIddictTokenManager _tokenManager;
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AuthService"/>.
    /// </summary>
    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictTokenManager tokenManager,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenManager = tokenManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CallerContext caller)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? request.Email.Split('@')[0]
                : request.DisplayName,
            Locale = request.Locale,
            Timezone = request.Timezone,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            _logger.LogWarning("Registration failed for {Email}: {Errors}", request.Email, errors);
            throw new InvalidOperationException($"Registration failed: {errors}");
        }

        _logger.LogInformation("User {UserId} registered with email {Email}", user.Id, user.Email);

        var requiresEmailConfirmation = _userManager.Options.SignIn.RequireConfirmedEmail;
        if (requiresEmailConfirmation)
        {
            var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            // TODO Phase 0.x: send confirmation email; for now log the token
            _logger.LogInformation(
                "Email confirmation token for {UserId}: {Token}", user.Id, confirmToken);
        }

        return new RegisterResponse
        {
            UserId = user.Id,
            Email = user.Email!,
            RequiresEmailConfirmation = requiresEmailConfirmation,
        };
    }

    /// <inheritdoc/>
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CallerContext caller)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            _logger.LogWarning("Login failed: user not found for email {Email}", request.Email);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: account {UserId} is inactive", user.Id);
            throw new UnauthorizedAccessException("Account is disabled.");
        }

        // Check lockout before validating password to avoid timing differences
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Login failed: account {UserId} is locked out", user.Id);
            throw new UnauthorizedAccessException("Account is locked. Try again later.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            _logger.LogWarning("Login failed: invalid password for user {UserId}", user.Id);
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        // Reset failed count on successful password check
        await _userManager.ResetAccessFailedCountAsync(user);

        // If user has MFA enabled and no TOTP code was supplied, signal that MFA is required
        if (await _userManager.GetTwoFactorEnabledAsync(user) &&
            string.IsNullOrEmpty(request.TotpCode))
        {
            _logger.LogInformation("MFA required for user {UserId}", user.Id);
            throw new InvalidOperationException("MFA_REQUIRED");
        }

        if (!string.IsNullOrEmpty(request.TotpCode))
        {
            var totpValid = await _userManager.VerifyTwoFactorTokenAsync(
                user,
                _userManager.Options.Tokens.AuthenticatorTokenProvider,
                request.TotpCode);

            if (!totpValid)
            {
                _logger.LogWarning("Login failed: invalid TOTP code for user {UserId}", user.Id);
                throw new UnauthorizedAccessException("Invalid MFA code.");
            }
        }

        _logger.LogInformation("User {UserId} authenticated successfully", user.Id);

        // Actual JWT token issuance happens at the HTTP endpoint layer via OpenIddict.
        // Return user identity information; the endpoint layer populates token fields.
        return new LoginResponse
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            AccessToken = string.Empty,   // Populated by OpenIddict endpoint
            RefreshToken = string.Empty,  // Populated by OpenIddict endpoint
            ExpiresIn = 0,
            TokenType = "Bearer",
        };
    }

    /// <inheritdoc/>
    public async Task LogoutAsync(Guid userId, string? refreshToken, CallerContext caller)
    {
        if (refreshToken is not null)
        {
            // Revoke the specific refresh token
            var token = await _tokenManager.FindByReferenceIdAsync(refreshToken);
            if (token is not null)
            {
                await _tokenManager.TryRevokeAsync(token);
                _logger.LogInformation("Revoked refresh token for user {UserId}", userId);
            }
        }
        else
        {
            // Revoke all tokens for the subject
            var subject = userId.ToString();
            await foreach (var token in _tokenManager.FindBySubjectAsync(subject))
            {
                await _tokenManager.TryRevokeAsync(token);
            }
            _logger.LogInformation("Revoked all tokens for user {UserId}", userId);
        }
    }

    /// <inheritdoc/>
    public async Task<TokenResponse> RefreshTokenAsync(RefreshTokenRequest request, CallerContext caller)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Validate the refresh token exists and is not revoked
        var token = await _tokenManager.FindByReferenceIdAsync(request.RefreshToken);
        if (token is null)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        var status = await _tokenManager.GetStatusAsync(token);
        if (status != OpenIddictConstants.Statuses.Valid)
        {
            throw new UnauthorizedAccessException("Refresh token has been revoked or is no longer valid.");
        }

        // Real token issuance happens at the HTTP endpoint layer.
        // Return placeholder; the endpoint layer uses OpenIddict to issue new tokens.
        return new TokenResponse
        {
            AccessToken = string.Empty,
            RefreshToken = string.Empty,
            ExpiresIn = 0,
            TokenType = "Bearer",
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmEmailAsync(Guid userId, string token)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        if (result.Succeeded)
        {
            _logger.LogInformation("Email confirmed for user {UserId}", userId);
        }

        return result.Succeeded;
    }

    /// <inheritdoc/>
    public async Task InitiatePasswordResetAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Don't reveal whether the email exists (security best practice)
            _logger.LogInformation("Password reset requested for unknown email {Email}", email);
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        // TODO Phase 0.x: send reset email; for now log the token
        _logger.LogInformation(
            "Password reset token for {UserId}: {Token}", user.Id, token);
    }

    /// <inheritdoc/>
    public async Task<bool> ResetPasswordAsync(ResetPasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return false;
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (result.Succeeded)
        {
            _logger.LogInformation("Password reset for user {UserId}", user.Id);
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Password reset failed for {UserId}: {Errors}", user.Id, errors);
        }

        return result.Succeeded;
    }
}
