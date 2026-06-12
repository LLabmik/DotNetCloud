using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Grpc.Lifecycle;
using HealthStatusGrpc = DotNetCloud.Core.Grpc.Lifecycle.HealthStatus;
using HealthStatusCore = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Services;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Globalization;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// Admin management endpoints for system settings, module management, health checks, and backups.
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
    private readonly IBackupService _backupService;
    private readonly IProcessSupervisor _supervisor;
    private readonly ILogger<AdminController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class.
    /// </summary>
    public AdminController(
        IAdminSettingsService settingsService,
        IAdminModuleService moduleService,
        HealthCheckService healthCheckService,
        IBackgroundServiceTracker backgroundServiceTracker,
        IBackupService backupService,
        IProcessSupervisor supervisor,
        ILogger<AdminController> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _moduleService = moduleService ?? throw new ArgumentNullException(nameof(moduleService));
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _backgroundServiceTracker = backgroundServiceTracker ?? throw new ArgumentNullException(nameof(backgroundServiceTracker));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
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

        var sanitizedModuleId = SanitizeForLog(moduleId);
        _logger.LogInformation("Module {ModuleId} started by admin", sanitizedModuleId);
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
        try
        {
            var stopped = await _moduleService.StopModuleAsync(moduleId, cancellationToken);
            if (!stopped)
            {
                return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
            }

            var sanitizedModuleId = SanitizeForLog(moduleId);
            _logger.LogInformation("Module {ModuleId} stopped by admin", sanitizedModuleId);
            return Ok(new { success = true, message = $"Module '{moduleId}' stopped successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "MODULE_REQUIRED", message = ex.Message } });
        }
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

        var sanitizedModuleId = SanitizeForLog(moduleId);
        _logger.LogInformation("Module {ModuleId} restarted by admin", sanitizedModuleId);
        return Ok(new { success = true, message = $"Module '{moduleId}' restarted successfully." });
    }

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
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
    // Backup & Restore
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Trigger an immediate backup of the DotNetCloud instance.
    /// Uses the configured backup settings from system settings.
    /// </summary>
    /// <param name="outputPath">Optional explicit output path for the backup archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The backup result with archive path and file count.</returns>
    [HttpPost("backup/run")]
    public async Task<IActionResult> RunBackupAsync(
        [FromQuery] string? outputPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Backup triggered by admin");

        // Read settings from system settings to build BackupOptions
        var settings = await _settingsService.ListSettingsAsync("dotnetcloud.core");
        var options = BuildBackupOptionsFromSettings(settings, outputPath);

        var result = await _backupService.CreateBackupAsync(outputPath, options, cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Backup triggered by admin failed: {Error}", result.ErrorMessage);
            return StatusCode(500, new { success = false, error = new { code = "BACKUP_FAILED", message = result.ErrorMessage } });
        }

        _logger.LogInformation("Backup triggered by admin completed: {Path} ({Count} files, {Size:N0} bytes)",
            result.FilePath, result.FileCount, result.SizeBytes);
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Gets the current backup status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether a backup is running and info about the last backup.</returns>
    [HttpGet("backup/status")]
    public async Task<IActionResult> GetBackupStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = await _backupService.GetStatusAsync(cancellationToken);
        return Ok(new { success = true, data = status });
    }

    /// <summary>
    /// Restore from a backup archive.
    /// </summary>
    /// <param name="filePath">Path to the backup archive file on the server.</param>
    /// <param name="restoreDatabase">Whether to also restore the database from the archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The restore result.</returns>
    [HttpPost("backup/restore")]
    public async Task<IActionResult> RestoreBackupAsync(
        [FromQuery] string filePath,
        [FromQuery] bool restoreDatabase = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest(new { success = false, error = new { code = "INVALID_FILE", message = "File path is required." } });
        }

        _logger.LogWarning("Restore from backup triggered by admin: {FilePath}", filePath);

        var options = new RestoreOptions { RestoreDatabase = restoreDatabase };
        var result = await _backupService.RestoreBackupAsync(filePath, options, cancellationToken);

        if (!result.Success)
        {
            return StatusCode(500, new { success = false, error = new { code = "RESTORE_FAILED", message = result.ErrorMessage } });
        }

        return Ok(new { success = true, data = result });
    }

    private static BackupOptions BuildBackupOptionsFromSettings(IReadOnlyList<SystemSettingDto> settings, string? outputPath)
    {
        var options = new BackupOptions();

        foreach (var setting in settings)
        {
            switch (setting.Key)
            {
                case "Backup:IncludeDatabase":
                    options.IncludeDatabaseDump = bool.TryParse(setting.Value, out var includeDb) ? includeDb : true;
                    break;
                case "Backup:IncludeFileStorage":
                    options.IncludeFileStorage = bool.TryParse(setting.Value, out var includeFiles) ? includeFiles : true;
                    break;
                case "Backup:IncludeModuleData":
                    options.IncludeModuleData = bool.TryParse(setting.Value, out var includeModules) ? includeModules : true;
                    break;
                case "Backup:Directory":
                    options.BackupDirectory = setting.Value;
                    break;
            }
        }

        return options;
    }

    // ---------------------------------------------------------------------------
    // System Health
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Get detailed system health status including all health checks and per-module status.
    /// Each process-isolated module appears as a separate entry with its gRPC health status.
    /// </summary>
    /// <returns>System health report with individual check results.</returns>
    [HttpGet("health")]
    public async Task<IActionResult> GetHealthAsync()
    {
        var report = await _healthCheckService.CheckHealthAsync();

        // Start with standard health check entries, stored as object so
        // we can mix them with per-module ModuleEntry values.
        var entries = new Dictionary<string, object>();
        foreach (var (key, entry) in report.Entries)
        {
            entries[key] = new
            {
                status = entry.Status.ToString(),
                description = entry.Description,
                duration = entry.Duration.TotalMilliseconds,
                exception = entry.Exception?.Message,
                data = ConvertHealthData(entry.Data),
            };
        }

        // Compute overall status starting from the health check service report.
        var overallStatus = report.Status;

        // Query each process-isolated module via gRPC and add per-module entries,
        // tracking the worst status across all modules.
        var moduleEntries = await GetModuleHealthEntriesAsync();
        foreach (var moduleEntry in moduleEntries)
        {
            entries[moduleEntry.Key] = moduleEntry.Value;

            if (moduleEntry.Value.moduleStatus == HealthStatusCore.Unhealthy)
            {
                overallStatus = HealthStatusCore.Unhealthy;
            }
            else if (moduleEntry.Value.moduleStatus == HealthStatusCore.Degraded && overallStatus != HealthStatusCore.Unhealthy)
            {
                overallStatus = HealthStatusCore.Degraded;
            }
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                status = overallStatus.ToString(),
                entries,
            },
        });
    }

    /// <summary>
    /// Queries each process-isolated module via gRPC <c>ModuleLifecycle.HealthCheck()</c>
    /// and returns per-module health entries.
    /// </summary>
    private async Task<Dictionary<string, ModuleEntry>> GetModuleHealthEntriesAsync()
    {
        var modules = _supervisor.GetAllModuleInfo();
        if (modules.Count == 0)
            return [];

        var tasks = modules.Select(m => CheckSingleModuleHealthAsync(m));
        var results = await Task.WhenAll(tasks);

        return results.ToDictionary(r => $"module:{r.ModuleId}", r => new ModuleEntry
        {
            moduleStatus = r.Status,
            status = r.Status.ToString(),
            description = r.Description,
            duration = r.DurationMs,
            exception = null,
            data = r.Data,
        });
    }

    private async Task<ModuleHealthResult> CheckSingleModuleHealthAsync(ModuleProcessInfo moduleInfo)
    {
        var moduleId = moduleInfo.ModuleId;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // If the process isn't running, report unhealthy immediately.
        if (moduleInfo.Status != ModuleProcessStatus.Running &&
            moduleInfo.Status != ModuleProcessStatus.Degraded)
        {
            sw.Stop();
            return new ModuleHealthResult
            {
                ModuleId = moduleId,
                Status = HealthStatusCore.Unhealthy,
                Description = $"Process status is '{moduleInfo.Status}'",
                DurationMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        var endpoint = moduleInfo.GrpcEndpoint;
        if (string.IsNullOrEmpty(endpoint))
        {
            sw.Stop();
            return new ModuleHealthResult
            {
                ModuleId = moduleId,
                Status = HealthStatusCore.Unhealthy,
                Description = "No gRPC endpoint available",
                DurationMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        try
        {
            using var channel = CreateGrpcChannel(endpoint);
            var client = new ModuleLifecycle.ModuleLifecycleClient(channel);
            var callOptions = new global::Grpc.Core.CallOptions(
                deadline: DateTime.UtcNow.AddSeconds(5));

            var response = await client.HealthCheckAsync(new HealthCheckRequest(), callOptions);
            sw.Stop();

            var status = response.Status switch
            {
                HealthStatusGrpc.Healthy => HealthStatusCore.Healthy,
                HealthStatusGrpc.Degraded => HealthStatusCore.Degraded,
                _ => HealthStatusCore.Unhealthy
            };

            var data = new Dictionary<string, object?>
            {
                ["module_id"] = moduleId,
                ["module_name"] = moduleInfo.ModuleName,
                ["version"] = moduleInfo.Version,
                ["process_status"] = moduleInfo.Status.ToString(),
            };

            return new ModuleHealthResult
            {
                ModuleId = moduleId,
                Status = status,
                Description = string.IsNullOrEmpty(response.Description)
                    ? $"gRPC health check returned {response.Status}"
                    : response.Description,
                DurationMs = sw.Elapsed.TotalMilliseconds,
                Data = data,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ModuleHealthResult
            {
                ModuleId = moduleId,
                Status = HealthStatusCore.Unhealthy,
                Description = $"gRPC health check failed: {ex.Message}",
                DurationMs = sw.Elapsed.TotalMilliseconds,
            };
        }
    }

    private static GrpcChannel CreateGrpcChannel(string endpoint)
    {
        var handler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        };

        return GrpcChannel.ForAddress(endpoint, new GrpcChannelOptions
        {
            HttpHandler = handler,
            MaxReceiveMessageSize = 16 * 1024 * 1024,
            MaxSendMessageSize = 16 * 1024 * 1024,
        });
    }

    /// <summary>
    /// Describes a single module health entry for the admin health response.
    /// </summary>
    private sealed class ModuleEntry
    {
        public required HealthStatusCore moduleStatus { get; init; }
        public required string status { get; init; }
        public required string description { get; init; }
        public required double duration { get; init; }
        public required string? exception { get; init; }
        public required IReadOnlyDictionary<string, object?>? data { get; init; }
    }

    private sealed class ModuleHealthResult
    {
        public required string ModuleId { get; init; }
        public required HealthStatusCore Status { get; init; }
        public required string Description { get; init; }
        public required double DurationMs { get; init; }
        public IReadOnlyDictionary<string, object?>? Data { get; init; }
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
