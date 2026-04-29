using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Data.Entities.Identity;
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
    private readonly ILogger<AuthSessionController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthSessionController"/> class.
    /// </summary>
    public AuthSessionController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AuthSessionController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
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
                var target = await ResolvePostLoginTargetAsync(email, returnUrl);
                return LocalRedirect(target);
            }

            if (result.RequiresTwoFactor)
            {
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
            _logger.LogError(ex, "Form login failed for {Email}", email);
            return RedirectToLogin($"Login error: {ex.GetType().Name}", returnUrl, email);
        }
    }

    private IActionResult RedirectToLogin(string error, string? returnUrl, string? email)
    {
        var safeReturn = IsSafeLocalReturnUrl(returnUrl) ? returnUrl! : "/";
        var encodedError = Uri.EscapeDataString(error);
        var encodedReturn = Uri.EscapeDataString(safeReturn);
        var encodedEmail = Uri.EscapeDataString(email ?? string.Empty);
        return LocalRedirect($"/auth/login?returnUrl={encodedReturn}&error={encodedError}&email={encodedEmail}");
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
            _logger.LogWarning("Could not persist LastLoginAt for {Email}: user not found", email);
    }
}
