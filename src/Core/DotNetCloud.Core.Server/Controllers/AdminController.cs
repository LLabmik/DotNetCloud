using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Globalization;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Admin management endpoints for system settings, module management, and health checks.
/// </summary>
[ApiController]
[Route("api/v1/core/admin")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public class AdminController : ControllerBase
{
    private readonly IAdminSettingsService _settingsService;
    private readonly IAdminModuleService _moduleService;
    private readonly HealthCheckService _healthCheckService;
    private readonly IBackgroundServiceTracker _backgroundServiceTracker;
    private readonly ILogger<AdminController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class.
    /// </summary>
    public AdminController(
        IAdminSettingsService settingsService,
        IAdminModuleService moduleService,
        HealthCheckService healthCheckService,
        IBackgroundServiceTracker backgroundServiceTracker,
        ILogger<AdminController> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _moduleService = moduleService ?? throw new ArgumentNullException(nameof(moduleService));
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _backgroundServiceTracker = backgroundServiceTracker ?? throw new ArgumentNullException(nameof(backgroundServiceTracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ---------------------------------------------------------------------------
    // Settings Management
    // ---------------------------------------------------------------------------

    /// <summary>
    /// List all system settings, optionally filtered by module.
    /// </summary>
    /// <param name="module">Optional module filter.</param>
    /// <returns>A list of system settings.</returns>
    [HttpGet("settings")]
    public async Task<IActionResult> ListSettingsAsync([FromQuery] string? module = null)
    {
        var settings = await _settingsService.ListSettingsAsync(module);
        return Ok(new { success = true, data = settings });
    }

    /// <summary>
    /// Get a specific system setting by module and key.
    /// </summary>
    /// <param name="module">The module that owns the setting.</param>
    /// <param name="key">The setting key.</param>
    /// <returns>The setting value and metadata.</returns>
    [HttpGet("settings/{module}/{key}")]
    public async Task<IActionResult> GetSettingAsync(string module, string key)
    {
        var setting = await _settingsService.GetSettingAsync(module, key);
        if (setting is null)
        {
            return NotFound(new { success = false, error = new { code = "SETTING_NOT_FOUND", message = $"Setting '{module}:{key}' not found." } });
        }

        return Ok(new { success = true, data = setting });
    }

    /// <summary>
    /// Create or update a system setting.
    /// </summary>
    /// <param name="module">The module that owns the setting.</param>
    /// <param name="key">The setting key.</param>
    /// <param name="dto">The setting value and metadata.</param>
    /// <returns>The created or updated setting.</returns>
    [HttpPut("settings/{module}/{key}")]
    public async Task<IActionResult> UpsertSettingAsync(string module, string key, [FromBody] UpsertSystemSettingDto dto)
    {
        var setting = await _settingsService.UpsertSettingAsync(module, key, dto);

        _logger.LogInformation("Setting {Module}:{Key} updated by admin", module, key);
        return Ok(new { success = true, data = setting });
    }

    /// <summary>
    /// Delete a system setting.
    /// </summary>
    /// <param name="module">The module that owns the setting.</param>
    /// <param name="key">The setting key.</param>
    /// <returns>Confirmation that the setting was deleted.</returns>
    [HttpDelete("settings/{module}/{key}")]
    public async Task<IActionResult> DeleteSettingAsync(string module, string key)
    {
        var deleted = await _settingsService.DeleteSettingAsync(module, key);
        if (!deleted)
        {
            return NotFound(new { success = false, error = new { code = "SETTING_NOT_FOUND", message = $"Setting '{module}:{key}' not found." } });
        }

        _logger.LogInformation("Setting {Module}:{Key} deleted by admin", module, key);
        return Ok(new { success = true, message = "Setting deleted successfully." });
    }

    // ---------------------------------------------------------------------------
    // Module Management
    // ---------------------------------------------------------------------------

    /// <summary>
    /// List all installed modules.
    /// </summary>
    /// <returns>A list of installed modules with their status and capabilities.</returns>
    [HttpGet("modules")]
    public async Task<IActionResult> ListModulesAsync()
    {
        var modules = await _moduleService.ListModulesAsync();
        return Ok(new { success = true, data = modules });
    }

    /// <summary>
    /// Get details for a specific installed module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <returns>The module details.</returns>
    [HttpGet("modules/{moduleId}")]
    public async Task<IActionResult> GetModuleAsync(string moduleId)
    {
        var module = await _moduleService.GetModuleAsync(moduleId);
        if (module is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        return Ok(new { success = true, data = module });
    }

    /// <summary>
    /// Start a specific module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Confirmation that the module was started.</returns>
    [HttpPost("modules/{moduleId}/start")]
    public async Task<IActionResult> StartModuleAsync(string moduleId, CancellationToken cancellationToken)
    {
        var started = await _moduleService.StartModuleAsync(moduleId, cancellationToken);
        if (!started)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        _logger.LogInformation("Module {ModuleId} started by admin", moduleId);
        return Ok(new { success = true, message = $"Module '{moduleId}' started successfully." });
    }

    /// <summary>
    /// Stop a specific module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Confirmation that the module was stopped.</returns>
    [HttpPost("modules/{moduleId}/stop")]
    public async Task<IActionResult> StopModuleAsync(string moduleId, CancellationToken cancellationToken)
    {
        var stopped = await _moduleService.StopModuleAsync(moduleId, cancellationToken);
        if (!stopped)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        _logger.LogInformation("Module {ModuleId} stopped by admin", moduleId);
        return Ok(new { success = true, message = $"Module '{moduleId}' stopped successfully." });
    }

    /// <summary>
    /// Restart a specific module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Confirmation that the module was restarted.</returns>
    [HttpPost("modules/{moduleId}/restart")]
    public async Task<IActionResult> RestartModuleAsync(string moduleId, CancellationToken cancellationToken)
    {
        var restarted = await _moduleService.RestartModuleAsync(moduleId, cancellationToken);
        if (!restarted)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        _logger.LogInformation("Module {ModuleId} restarted by admin", moduleId);
        return Ok(new { success = true, message = $"Module '{moduleId}' restarted successfully." });
    }

    /// <summary>
    /// Grant a capability to a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="capability">The capability name to grant.</param>
    /// <returns>The capability grant details.</returns>
    [HttpPost("modules/{moduleId}/capabilities/{capability}/grant")]
    public async Task<IActionResult> GrantCapabilityAsync(string moduleId, string capability)
    {
        if (!TryGetUserId(out var adminUserId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var grant = await _moduleService.GrantCapabilityAsync(moduleId, capability, adminUserId);
        if (grant is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        _logger.LogInformation("Capability {Capability} granted to module {ModuleId} by admin {AdminUserId}",
            capability, moduleId, adminUserId);
        return Ok(new { success = true, data = grant });
    }

    /// <summary>
    /// Revoke a capability from a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="capability">The capability name to revoke.</param>
    /// <returns>Confirmation that the capability was revoked.</returns>
    [HttpDelete("modules/{moduleId}/capabilities/{capability}")]
    public async Task<IActionResult> RevokeCapabilityAsync(string moduleId, string capability)
    {
        var revoked = await _moduleService.RevokeCapabilityAsync(moduleId, capability);
        if (!revoked)
        {
            return NotFound(new { success = false, error = new { code = "CAPABILITY_NOT_FOUND", message = $"Capability '{capability}' not found for module '{moduleId}'." } });
        }

        _logger.LogInformation("Capability {Capability} revoked from module {ModuleId} by admin",
            capability, moduleId);
        return Ok(new { success = true, message = $"Capability '{capability}' revoked from module '{moduleId}'." });
    }

    // ---------------------------------------------------------------------------
    // System Health
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Get detailed system health status including all health checks.
    /// </summary>
    /// <returns>System health report with individual check results.</returns>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealthAsync()
    {
        var report = await _healthCheckService.CheckHealthAsync();

        var entries = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => new
            {
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.TotalMilliseconds,
                exception = entry.Value.Exception?.Message,
                data = ConvertHealthData(entry.Value.Data),
            });

        return Ok(new
        {
            success = true,
            data = new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration.TotalMilliseconds,
                entries,
            },
        });
    }

    // ---------------------------------------------------------------------------
    // Background Services
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Get status of all tracked background services (last run time, duration, success).
    /// </summary>
    /// <returns>List of background service statuses.</returns>
    [HttpGet("background-services")]
    public IActionResult GetBackgroundServices()
    {
        var services = _backgroundServiceTracker.GetAll()
            .Values
            .OrderBy(s => s.ServiceName)
            .Select(s => new
            {
                serviceName = s.ServiceName,
                lastRunAt = s.LastRunAt,
                lastRunDurationMs = s.LastRunDuration.TotalMilliseconds,
                lastRunSuccess = s.LastRunSuccess,
                lastRunMessage = s.LastRunMessage,
                totalRuns = s.TotalRuns,
                totalFailures = s.TotalFailures,
            });

        return Ok(new { success = true, data = services });
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out userId);
    }

    private static IReadOnlyDictionary<string, object?>? ConvertHealthData(IReadOnlyDictionary<string, object> data)
    {
        if (data.Count == 0)
        {
            return null;
        }

        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in data)
        {
            normalized[key] = NormalizeHealthValue(value);
        }

        return normalized;
    }

    private static object? NormalizeHealthValue(object? value)
    {
        return value switch
        {
            null => null,
            string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or nint or nuint or float or double or decimal => value,
            Guid guid => guid.ToString(),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
            Uri uri => uri.ToString(),
            Enum enumValue => enumValue.ToString(),
            _ => value.ToString(),
        };
    }
}
