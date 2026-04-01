using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Endpoints for authenticated user-scoped settings.
/// </summary>
[ApiController]
[Route("api/v1/core/user-settings")]
[Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
public sealed class UserSettingsController : ControllerBase
{
    private readonly IUserSettingsService _userSettingsService;
    private readonly ILogger<UserSettingsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSettingsController"/> class.
    /// </summary>
    public UserSettingsController(IUserSettingsService userSettingsService, ILogger<UserSettingsController> logger)
    {
        _userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a specific setting for the current authenticated user.
    /// </summary>
    [HttpGet("{module}/{key}")]
    public async Task<IActionResult> GetSettingAsync(string module, string key)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var setting = await _userSettingsService.GetSettingAsync(userId, module, key);
        if (setting is null)
        {
            return NotFound(new { success = false, error = new { code = "SETTING_NOT_FOUND", message = $"User setting '{module}:{key}' not found." } });
        }

        return Ok(new { success = true, data = setting });
    }

    /// <summary>
    /// Creates or updates a setting for the current authenticated user.
    /// </summary>
    [HttpPut("{module}/{key}")]
    public async Task<IActionResult> UpsertSettingAsync(string module, string key, [FromBody] UpsertUserSettingDto dto)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var setting = await _userSettingsService.UpsertSettingAsync(userId, module, key, dto);

        _logger.LogInformation("User setting {Module}:{Key} updated for user {UserId}", module, key, userId);
        return Ok(new { success = true, data = setting });
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var claimValue = User.FindFirst("sub")?.Value
            ?? User.FindFirst("user_id")?.Value
            ?? User.FindFirst("nameidentifier")?.Value;

        return claimValue is not null && Guid.TryParse(claimValue, out userId);
    }
}
