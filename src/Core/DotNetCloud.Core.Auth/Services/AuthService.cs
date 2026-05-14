using System.Web;
using DotNetCloud.Core.Auth.Configuration;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Constants;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly IAdminSettingsService _adminSettingsService;
    private readonly ITransactionalEmailSender _emailSender;
    private readonly SmtpOptions _smtpOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AuthService"/>.
    /// </summary>
    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictTokenManager tokenManager,
        IAdminSettingsService adminSettingsService,
        ITransactionalEmailSender emailSender,
        IOptions<SmtpOptions> smtpOptions,
        IServiceProvider serviceProvider,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenManager = tokenManager;
        _adminSettingsService = adminSettingsService;
        _emailSender = emailSender;
        _smtpOptions = smtpOptions.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CallerContext caller)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check closed system mode — block self-registration if enabled
        var closedSetting = await _adminSettingsService.GetSettingAsync(
            SystemSettingKeys.CoreModule, SystemSettingKeys.ClosedSystemEnabled);

        if (closedSetting?.Value == "true" && !caller.HasRole("Administrator"))
        {
            _logger.LogWarning(
                "Registration blocked: closed system mode is enabled and caller is not an administrator");
            throw new InvalidOperationException(
                "Self-registration is disabled. Contact your system administrator to request an account.");
        }

        // Check demo mode — self-registered users become demo users
        var demoSetting = await _adminSettingsService.GetSettingAsync(
            SystemSettingKeys.CoreModule, SystemSettingKeys.DemoModeEnabled);

        var isDemoMode = demoSetting?.Value == "true";
        var isAdmin = caller.HasRole("Administrator");

        // Defense in depth: mutual exclusion check
        if (isDemoMode && closedSetting?.Value == "true")
        {
            _logger.LogError(
                "Configuration error: both DemoModeEnabled and ClosedSystemEnabled are true");
            throw new InvalidOperationException(
                "System configuration error: Demo Mode and Closed System cannot both be enabled.");
        }

        var isDemoUser = isDemoMode && !isAdmin;

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? request.Email.Split('@')[0]
                : request.DisplayName,
            Locale = request.Locale,
            Timezone = request.Timezone,
            IsDemoUser = isDemoUser,
        };

        // Use PasswordChangeRequired from the request (set by admin form).
        // Non-admin callers (self-registration) leave this false by default.
        if (isAdmin)
        {
            user.PasswordChangeRequired = request.PasswordChangeRequired;
        }

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            _logger.LogWarning("Registration failed for {Email}: {Errors}", request.Email, errors);
            throw new InvalidOperationException($"Registration failed: {errors}");
        }

        _logger.LogInformation(
            "User {UserId} registered with email {Email} (DemoUser={IsDemoUser})",
            user.Id, user.Email, isDemoUser);

        // Set 750 MB quota for demo users
        if (isDemoUser)
        {
            await SetDemoUserQuotaAsync(user.Id, caller);
        }

        var requiresEmailConfirmation = _userManager.Options.SignIn.RequireConfirmedEmail;
        if (requiresEmailConfirmation)
        {
            var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            _logger.LogInformation(
                "Email confirmation token generated for user {UserId}", user.Id);

            var encodedToken = HttpUtility.UrlEncode(confirmToken);
            var confirmUrl = $"{_smtpOptions.BaseUrl.TrimEnd('/')}/auth/confirm-email?userId={user.Id}&token={encodedToken}";
            var htmlBody = $"""
                <p>Welcome to DotNetCloud!</p>
                <p>Please confirm your email address by clicking the link below:</p>
                <p><a href="{confirmUrl}">Confirm Email</a></p>
                <p>If the link does not work, copy and paste this URL into your browser:</p>
                <p>{confirmUrl}</p>
                """;

            try
            {
                await _emailSender.SendAsync(user.Email!, user.DisplayName ?? user.Email!,
                    "Confirm your DotNetCloud account", htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email");
            }
        }

        return new RegisterResponse
        {
            UserId = user.Id,
            Email = user.Email!,
            RequiresEmailConfirmation = requiresEmailConfirmation,
        };
    }

    /// <summary>
    /// Sets a 750 MB storage quota for a newly registered demo user.
    /// Resolves <c>IQuotaService</c> from the Files module at runtime via
    /// <see cref="IServiceProvider"/> to avoid a compile-time project reference.
    /// </summary>
    private async Task SetDemoUserQuotaAsync(Guid userId, CallerContext caller)
    {
        const long demoQuotaBytes = 750L * 1024 * 1024; // 786,432,000 bytes

        try
        {
            // Resolve IQuotaService at runtime (lives in Files module, not a
            // compile-time dependency of Core.Auth). Using the interface from
            // the shared SDK is not possible, so we use reflection-free
            // service locator with the concrete interface name.
            var quotaServiceType = Type.GetType(
                "DotNetCloud.Modules.Files.Services.IQuotaService, DotNetCloud.Modules.Files",
                throwOnError: false);

            if (quotaServiceType is not null)
            {
                var quotaService = _serviceProvider.GetService(quotaServiceType);
                if (quotaService is not null)
                {
                    var setQuotaMethod = quotaServiceType.GetMethod("SetQuotaAsync");
                    if (setQuotaMethod is not null)
                    {
                        var task = (Task)setQuotaMethod.Invoke(quotaService,
                            [userId, demoQuotaBytes, caller, CancellationToken.None])!;
                        await task;

                        _logger.LogInformation(
                            "Set {QuotaBytes} bytes demo quota for user {UserId}",
                            demoQuotaBytes,
                            userId);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "IQuotaService not available in DI; demo quota not set for user {UserId}",
                        userId);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Files module not loaded; demo quota not set for user {UserId}",
                    userId);
            }
        }
        catch (Exception ex)
        {
            // Quota setting is best-effort for demo mode; don't fail registration
            _logger.LogWarning(
                ex,
                "Failed to set demo quota for user {UserId}",
                userId);
        }
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

        // In closed system mode, do not issue tokens until password is changed
        if (user.PasswordChangeRequired)
        {
            _logger.LogWarning(
                "Login blocked for user {UserId}: password change required (closed system mode)", user.Id);
            throw new InvalidOperationException("PASSWORD_CHANGE_REQUIRED");
        }

        user.LastLoginAt = DateTime.UtcNow;
        var lastLoginUpdate = await _userManager.UpdateAsync(user);
        if (!lastLoginUpdate.Succeeded)
        {
            var errors = string.Join(", ", lastLoginUpdate.Errors.Select(e => e.Description));
            _logger.LogWarning("Could not update LastLoginAt for user {UserId}: {Errors}", user.Id, errors);
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
        _logger.LogInformation(
            "Password reset token generated for user {UserId}", user.Id);

        var encodedToken = HttpUtility.UrlEncode(token);
        var encodedEmail = HttpUtility.UrlEncode(email);
        var resetUrl = $"{_smtpOptions.BaseUrl.TrimEnd('/')}/auth/reset-password?email={encodedEmail}&token={encodedToken}";
        var htmlBody = $"""
            <p>A password reset was requested for your DotNetCloud account.</p>
            <p>Click the link below to reset your password:</p>
            <p><a href="{resetUrl}">Reset Password</a></p>
            <p>If the link does not work, copy and paste this URL into your browser:</p>
            <p>{resetUrl}</p>
            <p>If you did not request this, you can safely ignore this email.</p>
            """;

        try
        {
            await _emailSender.SendAsync(user.Email!, user.DisplayName ?? user.Email!,
                "Reset your DotNetCloud password", htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
        }
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

    /// <inheritdoc/>
    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            _logger.LogWarning("Password change failed: user {UserId} not found", userId);
            return false;
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (result.Succeeded)
        {
            _logger.LogInformation("Password changed for user {UserId}", userId);
        }
        else
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Password change failed for {UserId}: {Errors}", userId, errors);
        }

        return result.Succeeded;
    }

    /// <inheritdoc/>
    public async Task<UserProfileResponse?> GetUserProfileAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        var isMfaEnabled = await _userManager.GetTwoFactorEnabledAsync(user);

        return new UserProfileResponse
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Locale = user.Locale,
            Timezone = user.Timezone,
            Roles = roles.ToList().AsReadOnly(),
            IsMfaEnabled = isMfaEnabled,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
        };
    }
}
