using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Constants;
using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Handles form-post authentication flows for the web UI.
/// </summary>
[ApiController]
[Route("auth/session")]
public sealed class AuthSessionController : ControllerBase
{
    private const string AdminBasePath = "/admin";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAdminSettingsService _adminSettings;
    private readonly ILogger<AuthSessionController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthSessionController"/> class.
    /// </summary>
    public AuthSessionController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAdminSettingsService adminSettings,
        ILogger<AuthSessionController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _adminSettings = adminSettings;
        _logger = logger;
    }

    /// <summary>
    /// Signs in using a normal HTTP form post so auth cookies can be written before response body starts.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginAsync(
        [FromForm] string email,
        [FromForm] string password,
        [FromForm] string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return RedirectToLogin("Email and password are required.", returnUrl, email);

        try
        {
            var result = await _signInManager.PasswordSignInAsync(
                email,
                password,
                isPersistent: true,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await UpdateLastLoginAsync(email);

                // Check if password change is required (closed system mode)
                var user = await _userManager.FindByEmailAsync(email);
                if (user is not null && user.PasswordChangeRequired)
                {
                    _logger.LogInformation(
                        "Password change required for user {UserId}, redirecting to change-password page", user.Id);
                    var safeReturn = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/";
                    var encodedReturn = Uri.EscapeDataString(safeReturn);
                    return LocalRedirect($"/auth/change-password?returnUrl={encodedReturn}");
                }

                // Redirect to MFA setup if AdminMfaRequired system setting is enabled
                // and this admin user hasn't completed TOTP enrollment yet.
                // This applies to ALL admin users (existing and future), unlike the
                // per-user MfaSetupRequired flag which only worked for the first seeded admin.
                if (user is not null && !await _userManager.GetTwoFactorEnabledAsync(user))
                {
                    var isAdmin = await _userManager.IsInRoleAsync(user, SystemRoleNames.Administrator);
                    if (isAdmin)
                    {
                        var mfaSetting = await _adminSettings.GetSettingAsync(
                            SystemSettingKeys.CoreModule, SystemSettingKeys.AdminMfaRequired);
                        if (mfaSetting?.Value == "true")
                        {
                            _logger.LogInformation(
                                "AdminMfaRequired enabled: redirecting admin user {UserId} to MFA setup", user.Id);
                            return LocalRedirect("/auth/mfa-setup");
                        }
                    }
                }

                var target = await ResolvePostLoginTargetAsync(email, returnUrl);
                return LocalRedirect(target);
            }

            if (result.RequiresTwoFactor)
            {
                _logger.LogInformation("Login requires 2FA for {Email}, redirecting to MFA verify", email);
                var mfaReturnUrl = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/";
                return LocalRedirect($"/auth/mfa-verify?returnUrl={Uri.EscapeDataString(mfaReturnUrl)}");
            }

            if (result.IsLockedOut)
                return RedirectToLogin("Account locked. Please try again later.", returnUrl, email);

            if (result.IsNotAllowed)
                return RedirectToLogin("Login not allowed. Please confirm your email.", returnUrl, email);

            return RedirectToLogin("Invalid email or password.", returnUrl, email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Form login failed");
            return RedirectToLogin($"Login error: {ex.GetType().Name}", returnUrl, email);
        }
    }

    /// <summary>
    /// Changes the current user's password (form-post flow).
    /// Used for the forced password change on first login in closed system mode.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePasswordPostAsync(
        [FromForm] string currentPassword,
        [FromForm] string newPassword,
        [FromForm] string confirmNewPassword,
        [FromForm] string? returnUrl = null)
    {
        var safeReturn = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/";

        if (string.IsNullOrWhiteSpace(currentPassword))
            return RedirectToChangePassword("Current password is required.", safeReturn);

        if (string.IsNullOrWhiteSpace(newPassword))
            return RedirectToChangePassword("New password is required.", safeReturn);

        if (newPassword != confirmNewPassword)
            return RedirectToChangePassword("New passwords do not match.", safeReturn);

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out var userGuid))
            return RedirectToChangePassword("Authentication error. Please sign out and try again.", safeReturn);

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return RedirectToChangePassword("User not found. Please sign out and try again.", safeReturn);

        var changeResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changeResult.Succeeded)
        {
            var errors = string.Join(" ", changeResult.Errors.Select(e => e.Description));
            _logger.LogWarning("Password change failed: {Errors}", errors);
            return RedirectToChangePassword(errors, safeReturn);
        }

        user.PasswordChangeRequired = false;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password changed for user {UserId} (forced change)", user.Id);

        var encodedReturn = Uri.EscapeDataString(safeReturn);
        return LocalRedirect($"/auth/change-password?returnUrl={encodedReturn}&changed=true");
    }

    /// <summary>
    /// Verifies a TOTP code from the authenticator app via form POST.
    /// Must be a real HTTP POST (not Blazor SignalR) so the auth cookie can be set on the response.
    /// </summary>
    [HttpPost("mfa-verify")]
    [AllowAnonymous]
    public async Task<IActionResult> MfaVerifyAsync(
        [FromForm] string code,
        [FromForm] string? returnUrl = null)
    {
        var safeReturn = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/";

        if (string.IsNullOrWhiteSpace(code))
            return RedirectToMfaVerify("Verification code is required.", safeReturn);

        try
        {
            var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
                code, isPersistent: true, rememberClient: true);

            if (result.Succeeded)
            {
                var target = await ResolveMfaPostLoginTargetAsync(safeReturn);
                return LocalRedirect(target);
            }

            if (result.IsLockedOut)
                return RedirectToMfaVerify("Account locked. Please try again later.", safeReturn);

            return RedirectToMfaVerify("Invalid verification code.", safeReturn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MFA form verification failed");
            return RedirectToMfaVerify($"Verification error: {ex.GetType().Name}", safeReturn);
        }
    }

    /// <summary>
    /// Signs out the current user and clears the auth cookie via form POST.
    /// Must be a real HTTP POST (not Blazor SignalR) so the cookie can be cleared on the response.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> LogoutAsync([FromForm] string? returnUrl = null)
    {
        try
        {
            await _signInManager.SignOutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignOutAsync failed");
        }

        // Explicitly clear the auth cookie as well, since SignOutAsync may not
        // always produce a Set-Cookie header that the browser honors before the
        // redirect is followed (Blazor enhanced nav race condition).
        foreach (var cookie in Request.Cookies.Keys)
        {
            if (cookie.StartsWith(".AspNetCore.", StringComparison.OrdinalIgnoreCase)
                || cookie.StartsWith("__Host-.AspNetCore.", StringComparison.OrdinalIgnoreCase)
                || cookie.StartsWith("Identity.", StringComparison.OrdinalIgnoreCase))
            {
                Response.Cookies.Delete(cookie);
            }
        }

        _logger.LogInformation("User logged out via form POST");

        var safeReturn = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/auth/login";
        return LocalRedirect(safeReturn);
    }

    private IActionResult RedirectToLogin(string error, string? returnUrl, string? email)
    {
        var safeReturn = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/";
        var encodedError = Uri.EscapeDataString(error);
        var encodedReturn = Uri.EscapeDataString(safeReturn);
        var encodedEmail = Uri.EscapeDataString(email ?? string.Empty);
        return LocalRedirect($"/auth/login?returnUrl={encodedReturn}&error={encodedError}&email={encodedEmail}");
    }

    private IActionResult RedirectToChangePassword(string error, string? returnUrl)
    {
        var safeReturn = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/";
        var encodedError = Uri.EscapeDataString(error);
        var encodedReturn = Uri.EscapeDataString(safeReturn);
        return LocalRedirect($"/auth/change-password?returnUrl={encodedReturn}&error={encodedError}");
    }

    private IActionResult RedirectToMfaVerify(string error, string? returnUrl)
    {
        var safeReturn = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/";
        var encodedError = Uri.EscapeDataString(error);
        var encodedReturn = Uri.EscapeDataString(safeReturn);
        return LocalRedirect($"/auth/mfa-verify?returnUrl={encodedReturn}&error={encodedError}");
    }

    private static bool IsSafeLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith("/", StringComparison.Ordinal)
            && !returnUrl.StartsWith("//", StringComparison.Ordinal);
    }

    private async Task<string> ResolvePostLoginTargetAsync(string email, string? returnUrl)
    {
        var safeTarget = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/";
        if (!safeTarget.StartsWith(AdminBasePath, StringComparison.OrdinalIgnoreCase))
            return safeTarget;

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return "/";

        var isAdmin = await _userManager.IsInRoleAsync(user, SystemRoleNames.Administrator);

        return isAdmin ? safeTarget : "/";
    }

    private async Task<string> ResolveMfaPostLoginTargetAsync(string returnUrl)
    {
        if (!returnUrl.StartsWith(AdminBasePath, StringComparison.OrdinalIgnoreCase))
            return returnUrl;

        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
            return "/";

        var isAdmin = await _userManager.IsInRoleAsync(user, SystemRoleNames.Administrator);

        return isAdmin ? returnUrl : "/";
    }

    private async Task UpdateLastLoginAsync(string email)
    {
        // Use ExecuteUpdateAsync to avoid a second tracked instance conflicting with
        // the ApplicationUser already tracked by PasswordSignInAsync in the same DbContext scope.
        var now = DateTime.UtcNow;
        var normalizedEmail = _userManager.NormalizeEmail(email);
        var updated = await _userManager.Users
            .Where(u => u.NormalizedEmail == normalizedEmail)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, now));
        if (updated == 0)
            _logger.LogWarning("Could not persist LastLoginAt: user not found");
    }
}
