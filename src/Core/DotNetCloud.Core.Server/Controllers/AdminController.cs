using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.Auth.Security;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Constants;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Grpc.Lifecycle;
using HealthStatusGrpc = DotNetCloud.Core.Grpc.Lifecycle.HealthStatus;
using HealthStatusCore = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Modules.Supervisor;
using DotNetCloud.Core.Services;
using DotNetCloud.Core.Server.Services;
using DotNetCloud.ModulePackaging;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
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
    private readonly BackupJobRunner _backupRunner;
    private readonly IProcessSupervisor _supervisor;
    private readonly Supervisor.IModuleGrpcChannelProvider _channelProvider;
    private readonly IAuditLogger _auditLogger;
    private readonly IHostApplicationLifetime _hostLifetime;
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
        BackupJobRunner backupRunner,
        IProcessSupervisor supervisor,
        IAuditLogger auditLogger,
        IHostApplicationLifetime hostLifetime,
        Supervisor.IModuleGrpcChannelProvider channelProvider,
        ILogger<AdminController> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _moduleService = moduleService ?? throw new ArgumentNullException(nameof(moduleService));
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _backgroundServiceTracker = backgroundServiceTracker ?? throw new ArgumentNullException(nameof(backgroundServiceTracker));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _backupRunner = backupRunner ?? throw new ArgumentNullException(nameof(backupRunner));
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _channelProvider = channelProvider ?? throw new ArgumentNullException(nameof(channelProvider));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _hostLifetime = hostLifetime ?? throw new ArgumentNullException(nameof(hostLifetime));
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

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.Update,
            EntityType = "SystemSetting",
            EntityId = Guid.CreateVersion7(),
            Description = $"setting-upsert:{SanitizeForLog(module)}:{SanitizeForLog(key)}",
        });
        _logger.LogInformation("Setting {Module}:{Key} updated by admin", SanitizeForLog(module), SanitizeForLog(key));
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

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.Delete,
            EntityType = "SystemSetting",
            EntityId = Guid.CreateVersion7(),
            Description = $"setting-deleted:{SanitizeForLog(module)}:{SanitizeForLog(key)}",
        });
        _logger.LogInformation("Setting {Module}:{Key} deleted by admin", SanitizeForLog(module), SanitizeForLog(key));
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

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.ModuleUpdated,
            EntityType = "Module",
            EntityId = Guid.CreateVersion7(),
            Description = $"module-started:{SanitizeForLog(moduleId)}",
        });
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

            await _auditLogger.LogAsync(new AuditEntry
            {
                Caller = BuildCaller(),
                ModuleId = "dotnetcloud.core",
                Action = AuditAction.ModuleUpdated,
                EntityType = "Module",
                EntityId = Guid.CreateVersion7(),
                Description = $"module-stopped:{SanitizeForLog(moduleId)}",
            });
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

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.ModuleUpdated,
            EntityType = "Module",
            EntityId = Guid.CreateVersion7(),
            Description = $"module-restarted:{SanitizeForLog(moduleId)}",
        });
        var sanitizedModuleId = SanitizeForLog(moduleId);
        _logger.LogInformation("Module {ModuleId} restarted by admin", sanitizedModuleId);
        return Ok(new { success = true, message = $"Module '{moduleId}' restarted successfully." });
    }

    /// <summary>
    /// Get the isolated data store of a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The data-store state, including schema, principal and provisioning status.</returns>
    [HttpGet("modules/{moduleId}/datastore")]
    public async Task<IActionResult> GetModuleDataStoreAsync(string moduleId, CancellationToken cancellationToken)
    {
        var store = await _moduleService.GetDataStoreAsync(moduleId, cancellationToken);
        if (store is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        return Ok(new { success = true, data = store });
    }

    /// <summary>
    /// Provisions (or repairs) the schema and database principal for an isolated module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provisioning outcome. Never contains credentials.</returns>
    [HttpPost("modules/{moduleId}/datastore/provision")]
    public async Task<IActionResult> ProvisionModuleDataStoreAsync(string moduleId, CancellationToken cancellationToken)
    {
        var result = await _moduleService.ProvisionDataStoreAsync(moduleId, cancellationToken);
        if (result is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        if (!result.Success)
        {
            _logger.LogWarning("Data store provisioning for module {ModuleId} failed by admin request", SanitizeForLog(moduleId));
            await AuditDataStoreErrorAsync("provision", moduleId, result.Error);
            return BadRequest(new { success = false, error = new { code = "MODULE_DATASTORE_OPERATION_FAILED", message = result.Error } });
        }

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.ModuleDataStoreProvisioned,
            EntityType = "ModuleDataStore",
            EntityId = Guid.CreateVersion7(),
            Description = $"module-datastore-provisioned:{SanitizeForLog(moduleId)}",
        });
        _logger.LogInformation("Data store for module {ModuleId} provisioned by admin", SanitizeForLog(moduleId));
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Rotates the module database principal's password and re-encrypts the stored connection string.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rotation outcome. Never contains credentials.</returns>
    [HttpPost("modules/{moduleId}/datastore/rotate")]
    public async Task<IActionResult> RotateModuleDataStoreCredentialsAsync(string moduleId, CancellationToken cancellationToken)
    {
        var result = await _moduleService.RotateDataStoreCredentialsAsync(moduleId, cancellationToken);
        if (result is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        if (!result.Success)
        {
            _logger.LogWarning("Data store credential rotation for module {ModuleId} failed by admin request", SanitizeForLog(moduleId));
            await AuditDataStoreErrorAsync("rotate", moduleId, result.Error);
            return BadRequest(new { success = false, error = new { code = "MODULE_DATASTORE_OPERATION_FAILED", message = result.Error } });
        }

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.ModuleDataStoreProvisioned,
            EntityType = "ModuleDataStore",
            EntityId = Guid.CreateVersion7(),
            Description = $"module-datastore-credentials-rotated:{SanitizeForLog(moduleId)}",
        });
        _logger.LogInformation("Data store credentials for module {ModuleId} rotated by admin", SanitizeForLog(moduleId));
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Drops the module schema and database principal. Irreversible; all module data is destroyed.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The destruction outcome.</returns>
    [HttpDelete("modules/{moduleId}/datastore")]
    public async Task<IActionResult> DestroyModuleDataStoreAsync(string moduleId, CancellationToken cancellationToken)
    {
        var result = await _moduleService.DestroyDataStoreAsync(moduleId, cancellationToken);
        if (result is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        if (!result.Success)
        {
            _logger.LogWarning("Data store destruction for module {ModuleId} failed by admin request", SanitizeForLog(moduleId));
            await AuditDataStoreErrorAsync("destroy", moduleId, result.Error);
            return BadRequest(new { success = false, error = new { code = "MODULE_DATASTORE_OPERATION_FAILED", message = result.Error } });
        }

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.ModuleDataStoreDestroyed,
            EntityType = "ModuleDataStore",
            EntityId = Guid.CreateVersion7(),
            Description = $"module-datastore-destroyed:{SanitizeForLog(moduleId)}",
        });
        _logger.LogInformation("Data store for module {ModuleId} destroyed by admin", SanitizeForLog(moduleId));
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Gets the resource quota override and the effective quota of a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The quota state, including the effective values applied by the supervisor.</returns>
    [HttpGet("modules/{moduleId}/quota")]
    public async Task<IActionResult> GetModuleQuotaAsync(string moduleId, CancellationToken cancellationToken)
    {
        var quota = await _moduleService.GetQuotaAsync(moduleId, cancellationToken);
        if (quota is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        return Ok(new { success = true, data = quota });
    }

    /// <summary>
    /// Stores an admin resource-quota override for a module (F10).
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="dto">The replacement override values; null or zero clears a dimension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored quota and the effective values after policy clamping.</returns>
    [HttpPut("modules/{moduleId}/quota")]
    public async Task<IActionResult> UpdateModuleQuotaAsync(
        string moduleId, [FromBody] UpdateModuleQuotaDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var result = await _moduleService.UpdateQuotaAsync(moduleId, dto, adminUserId, cancellationToken);
        if (result is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        if (!result.Success)
        {
            return BadRequest(new { success = false, error = new { code = "MODULE_QUOTA_INVALID", message = result.Error } });
        }

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.Update,
            EntityType = "ModuleQuota",
            EntityId = Guid.CreateVersion7(),
            Description = $"module-quota-changed:{SanitizeForLog(moduleId)}",
        });
        _logger.LogInformation("Resource quota for module {ModuleId} updated by admin", SanitizeForLog(moduleId));
        return Ok(new { success = true, data = result.Quota });
    }

    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return LogSanitizer.Sanitize(value ?? string.Empty);
    }

    private CallerContext BuildCaller()
    {
        if (!TryGetUserId(out var userId) || userId == Guid.Empty)
            return CallerContext.CreateSystemContext();

        var roles = User.FindAll("role")
            .Concat(User.FindAll(System.Security.Claims.ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new CallerContext(userId, roles, CallerType.User);
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

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.Update,
            EntityType = "ModuleCapability",
            EntityId = Guid.CreateVersion7(),
            Description = $"capability-granted:{SanitizeForLog(moduleId)}:{SanitizeForLog(capability)}",
        });
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

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.Update,
            EntityType = "ModuleCapability",
            EntityId = Guid.CreateVersion7(),
            Description = $"capability-revoked:{SanitizeForLog(moduleId)}:{SanitizeForLog(capability)}",
        });
        _logger.LogInformation("Capability {Capability} revoked from module {ModuleId} by admin",
            capability, moduleId);
        return Ok(new { success = true, message = $"Capability '{capability}' revoked from module '{moduleId}'." });
    }

    // ---------------------------------------------------------------------------
    // Module lifecycle: uninstall, launch credentials, orphaned data stores
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Uninstall a module.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="dto">Whether to destroy the module's data and remove its binaries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The uninstall outcome, including whether the data store was destroyed or kept.</returns>
    [HttpPost("modules/{moduleId}/uninstall")]
    public async Task<IActionResult> UninstallModuleAsync(
        string moduleId, [FromBody] UninstallModuleDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var result = await _moduleService.UninstallModuleAsync(moduleId, dto, adminUserId, cancellationToken);
        if (result is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        if (!result.Success)
        {
            var code = RequiredModules.IsRequired(moduleId) ? "MODULE_REQUIRED" : "MODULE_UNINSTALL_FAILED";
            return BadRequest(new { success = false, error = new { code, message = result.Error } });
        }

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.ModuleUninstalled,
            EntityType = "Module",
            EntityId = Guid.CreateVersion7(),
            Description = result.DataStoreDestroyed
                ? $"module-uninstalled:data-destroyed:{SanitizeForLog(moduleId)}"
                : $"module-uninstalled:data-kept:{SanitizeForLog(moduleId)}",
        });
        _logger.LogInformation(
            "Module {ModuleId} uninstalled by admin (dataDestroyed={DataDestroyed})",
            SanitizeForLog(moduleId), result.DataStoreDestroyed);
        return Ok(new { success = true, data = result });
    }

    /// <summary>
    /// Gets the non-secret lifecycle record of a module's launch credential.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <returns>The credential record (never the token itself).</returns>
    [HttpGet("modules/{moduleId}/credential")]
    public async Task<IActionResult> GetModuleCredentialAsync(string moduleId)
    {
        var credential = await _moduleService.GetCredentialAsync(moduleId);
        if (credential is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        return Ok(new { success = true, data = credential });
    }

    /// <summary>
    /// Rotates a module's launch credential, restarting the module when it is running.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new credential record.</returns>
    [HttpPost("modules/{moduleId}/credential/rotate")]
    public async Task<IActionResult> RotateModuleCredentialAsync(string moduleId, CancellationToken cancellationToken)
    {
        var credential = await _moduleService.RotateCredentialAsync(moduleId, cancellationToken);
        if (credential is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.ModuleCredentialRotated,
            EntityType = "ModuleCredential",
            EntityId = Guid.CreateVersion7(),
            Description = $"module-credential-rotated:{SanitizeForLog(moduleId)}",
        });
        _logger.LogInformation("Launch credential for module {ModuleId} rotated by admin", SanitizeForLog(moduleId));
        return Ok(new { success = true, data = credential });
    }

    /// <summary>
    /// Revokes a module's launch credential. The running process is rejected until it restarts.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The revoked credential record.</returns>
    [HttpPost("modules/{moduleId}/credential/revoke")]
    public async Task<IActionResult> RevokeModuleCredentialAsync(string moduleId, CancellationToken cancellationToken)
    {
        var credential = await _moduleService.RevokeCredentialAsync(moduleId, cancellationToken);
        if (credential is null)
        {
            return NotFound(new { success = false, error = new { code = "MODULE_NOT_FOUND", message = $"Module '{moduleId}' not found." } });
        }

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.ModuleCredentialRotated,
            EntityType = "ModuleCredential",
            EntityId = Guid.CreateVersion7(),
            Description = $"module-credential-revoked:{SanitizeForLog(moduleId)}",
        });
        _logger.LogWarning("Launch credential for module {ModuleId} revoked by admin", SanitizeForLog(moduleId));
        return Ok(new { success = true, data = credential });
    }

    /// <summary>
    /// Lists data stores retained as orphaned after modules were uninstalled with "keep data".
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The orphaned stores an administrator can inspect and drop.</returns>
    [HttpGet("datastores/orphaned")]
    public async Task<IActionResult> ListOrphanedDataStoresAsync(CancellationToken cancellationToken)
    {
        var stores = await _moduleService.ListOrphanedDataStoresAsync(cancellationToken);
        return Ok(new { success = true, data = stores });
    }

    private Task AuditDataStoreErrorAsync(string operation, string moduleId, string? error) =>
        _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.ModuleDataStoreError,
            EntityType = "ModuleDataStore",
            EntityId = Guid.CreateVersion7(),
            Description = $"module-datastore-{operation}-failed:{SanitizeForLog(moduleId)}:{SanitizeForLog(error)}",
        });

    // ---------------------------------------------------------------------------
    // Module store (B3)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Lists the packages available from the configured module store source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The catalogue entries, annotated with the installed version where known.</returns>
    [HttpGet("modules/store")]
    public async Task<IActionResult> ListStorePackagesAsync(CancellationToken cancellationToken)
    {
        var packages = await _moduleService.ListStorePackagesAsync(cancellationToken);
        return Ok(new { success = true, data = packages });
    }

    /// <summary>
    /// Verifies an uploaded <c>.dncpkg</c> package without installing it.
    /// </summary>
    /// <param name="file">The package file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification findings and the manifest facts the administrator must review.</returns>
    [HttpPost("modules/install/inspect")]
    [RequestSizeLimit(ModulePackageLimits.MaxPackageBytes)]
    public async Task<IActionResult> InspectModulePackageAsync(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "PACKAGE_MISSING", message = "Upload a .dncpkg file." },
            });
        }

        var temporaryPath = await SaveUploadAsync(file, cancellationToken);

        try
        {
            var review = await _moduleService.InspectPackageAsync(temporaryPath, cancellationToken);
            return Ok(new { success = true, data = review });
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    /// <summary>
    /// Installs an uploaded <c>.dncpkg</c> package after an administrator reviewed it.
    /// </summary>
    /// <remarks>
    /// The form must carry the hash shown by the review and the exact capability and scope lists it
    /// displayed. Anything else is the wrong package or an unreviewed capability set and is refused.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The install outcome; a failed health check is rolled back before the response.</returns>
    [HttpPost("modules/install")]
    [RequestSizeLimit(ModulePackageLimits.MaxPackageBytes)]
    public async Task<IActionResult> InstallModulePackageAsync(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        if (!Request.HasFormContentType)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "INVALID_REQUEST", message = "Expected multipart/form-data." },
            });
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");

        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "PACKAGE_MISSING", message = "Upload a .dncpkg file." },
            });
        }

        var dto = new InstallModulePackageDto
        {
            ExpectedSha256 = form["expectedSha256"].FirstOrDefault(),
            AcknowledgedCapabilities = ReadFormList(form, "acknowledgedCapabilities"),
            AcknowledgedScopes = ReadFormList(form, "acknowledgedScopes"),
            AllowUnverified = ReadFormBool(form, "allowUnverified", defaultValue: false),
            StartAfterInstall = ReadFormBool(form, "startAfterInstall", defaultValue: true),
        };

        var temporaryPath = await SaveUploadAsync(file, cancellationToken);

        try
        {
            var result = await _moduleService.InstallPackageAsync(
                temporaryPath, dto, adminUserId, cancellationToken);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Package install failed for module {ModuleId}: {Error}",
                    SanitizeForLog(result.ModuleId), SanitizeForLog(result.Error));

                return BadRequest(new { success = false, data = result, error = new { code = "INSTALL_FAILED", message = result.Error } });
            }

            _logger.LogInformation(
                "Package install of {ModuleId} {Version} completed by admin {AdminUserId} (unverified={Unverified})",
                SanitizeForLog(result.ModuleId), SanitizeForLog(result.Version), adminUserId, dto.AllowUnverified);

            return Ok(new { success = true, data = result });
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    /// <summary>
    /// Restores the previous version retained by the last install.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rollback outcome.</returns>
    [HttpPost("modules/{moduleId}/rollback")]
    public async Task<IActionResult> RollbackModuleAsync(string moduleId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Invalid token claims" } });
        }

        var result = await _moduleService.RollbackModuleAsync(moduleId, adminUserId, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { success = false, data = result, error = new { code = "ROLLBACK_FAILED", message = result.Error } });
        }

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.ModuleUpdated,
            EntityType = "Module",
            EntityId = Guid.CreateVersion7(),
            Description = $"module-rollback:{SanitizeForLog(moduleId)}:{SanitizeForLog(result.Version)}",
        });

        _logger.LogInformation(
            "Module {ModuleId} rolled back to {Version} by admin {AdminUserId}",
            SanitizeForLog(moduleId), SanitizeForLog(result.Version), adminUserId);

        return Ok(new { success = true, data = result });
    }

    private async Task<string> SaveUploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(Path.GetTempPath(), "dnc-module-upload");
        Directory.CreateDirectory(directory);

        // Never trust the client's file name: the extension is fixed and the name is generated.
        var path = Path.Combine(directory, Guid.CreateVersion7().ToString("N") + ModulePackageFormat.Extension);

        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await file.CopyToAsync(stream, cancellationToken);

        return path;
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            // 'File' alone would resolve to ControllerBase.File(...).
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug("Could not delete the uploaded package {Path}: {Error}", path, ex.Message);
        }
    }

    private static IReadOnlyList<string> ReadFormList(IFormCollection form, string key) =>
        form[key]
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

    private static bool ReadFormBool(IFormCollection form, string key, bool defaultValue) =>
        form.TryGetValue(key, out var value) && bool.TryParse(value.FirstOrDefault(), out var parsed)
            ? parsed
            : defaultValue;

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
            SanitizeForLog(result.FilePath), result.FileCount, result.SizeBytes);
        return Ok(new { success = true, data = result });
    }

    // ---------------------------------------------------------------------------
    // Security — OpenIddict Key Rotation (SOC 2 CC6 / C1)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Rotates the OpenIddict signing and encryption keys immediately (SOC 2 CC6 / C1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Backs up the existing <c>oidc-keys/</c> directory to a timestamped sibling,
    /// generates fresh signing + encryption RSA keys via <see cref="OidcKeyManager"/>
    /// (date-stamped filenames), and cleans up keys past the retention window. The new
    /// keys become the active signing keys on the next server restart (keys are loaded
    /// at startup). The action is written to the audit trail.
    /// </para>
    /// <para>
    /// This is the manual/emergency rotation path; the platform also rotates keys
    /// automatically every 90 days via <c>OidcKeyRotationService</c>.
    /// </para>
    /// </remarks>
    /// <returns>The new key IDs, backup path, and a note.</returns>
    [HttpPost("security/rotate-oidc-keys")]
    public async Task<IActionResult> RotateOidcKeysAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var oidcKeysDir = OidcKeyManager.GetOidcKeysDirectory();
            Directory.CreateDirectory(oidcKeysDir);

            // 1. Back up the existing keys.
            var backupPath = string.Empty;
            var existing = Directory.GetFiles(oidcKeysDir, "*.pem");
            if (existing.Length > 0)
            {
                backupPath = Path.Combine(
                    Path.GetDirectoryName(oidcKeysDir) ?? oidcKeysDir,
                    $"oidc-keys-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}");
                Directory.CreateDirectory(backupPath);
                foreach (var f in existing)
                {
                    System.IO.File.Copy(f, Path.Combine(backupPath, Path.GetFileName(f)));
                }
            }

            // 2. Generate fresh signing + encryption keys.
            var signingKey = OidcKeyManager.GenerateRotatedKey(oidcKeysDir, OidcKeyManager.SigningKeyPrefix, _logger);
            var encryptionKey = OidcKeyManager.GenerateRotatedKey(oidcKeysDir, OidcKeyManager.EncryptionKeyPrefix, _logger);

            // 3. Clean up keys past the retention window (keeps at least the newest).
            OidcKeyManager.CleanupOldKeys(oidcKeysDir, OidcKeyManager.SigningKeyPrefix, TimeSpan.FromDays(120), _logger);
            OidcKeyManager.CleanupOldKeys(oidcKeysDir, OidcKeyManager.EncryptionKeyPrefix, TimeSpan.FromDays(120), _logger);

            // 4. Audit the rotation.
            await _auditLogger.LogAsync(new AuditEntry
            {
                Caller = BuildCaller(),
                ModuleId = "dotnetcloud.core",
                Action = AuditAction.Update,
                EntityType = "OidcKey",
                EntityId = Guid.CreateVersion7(),
                Description = $"oidc-key-rotation:signing={signingKey.KeyId}",
            }, cancellationToken);

            // 5. Flag that a restart is required to activate the new keys. Cleared
            //    automatically on the next server start.
            var rotatedAt = DateTime.UtcNow;
            await _settingsService.UpsertSettingAsync(
                SystemSettingKeys.CoreModule,
                SystemSettingKeys.OidcKeysPendingRestart,
                new UpsertSystemSettingDto
                {
                    Value = rotatedAt.ToString("O", CultureInfo.InvariantCulture),
                    Description = $"OpenIddict keys rotated; restart required to activate (signing={signingKey.KeyId}).",
                });

            var result = new OidcKeyRotationResult
            {
                SigningKeyId = signingKey.KeyId,
                EncryptionKeyId = encryptionKey.KeyId,
                BackupPath = backupPath,
                RotatedAtUtc = rotatedAt,
                Note = "New keys take effect on the next server restart.",
            };

            _logger.LogInformation(
                "OpenIddict keys rotated by admin: signing={SigningKeyId} backup={BackupPath}",
                signingKey.KeyId, string.IsNullOrEmpty(backupPath) ? "(none)" : backupPath);

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual OpenIddict key rotation failed.");
            return StatusCode(500, new { success = false, error = new { code = "KEY_ROTATION_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Gets the OpenIddict key-rotation status (SOC 2 CC6 / C1).
    /// </summary>
    /// <remarks>
    /// Reports whether a rotation occurred since the last server start, in which case a
    /// restart is required to activate the newest keys. The admin UI shows a banner when
    /// <c>restartPending</c> is true.
    /// </remarks>
    /// <returns>The rotation status.</returns>
    [HttpGet("security/oidc-key-rotation-status")]
    public async Task<IActionResult> GetOidcKeyRotationStatusAsync()
    {
        var status = new OidcKeyRotationStatus();
        var setting = await _settingsService.GetSettingAsync(
            SystemSettingKeys.CoreModule, SystemSettingKeys.OidcKeysPendingRestart);

        if (setting is not null && !string.IsNullOrWhiteSpace(setting.Value))
        {
            status.RestartPending = true;
            if (DateTime.TryParse(setting.Value, CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var rotatedAt))
            {
                status.RotatedAtUtc = rotatedAt.ToUniversalTime();
            }

            // The signing key id is embedded in the description, e.g.
            // "OpenIddict keys rotated; restart required to activate (signing=<kid>)."
            var desc = setting.Description;
            if (!string.IsNullOrWhiteSpace(desc) && desc.Contains("signing=", StringComparison.Ordinal))
            {
                var start = desc.IndexOf("signing=", StringComparison.Ordinal) + "signing=".Length;
                var end = desc.IndexOf(')', start);
                if (end > start)
                    status.SigningKeyId = desc[start..end];
            }
        }

        return Ok(new { success = true, data = status });
    }

    /// <summary>
    /// Gracefully restarts the DotNetCloud server (admin).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Returns <c>202 Accepted</c> immediately, then requests a graceful application stop
    /// after a short delay so the response is flushed. The process supervisor / systemd
    /// (<c>Restart=always</c>) brings the service back up. Use this after rotating OpenIddict
    /// keys to activate them, or for other maintenance.
    /// </para>
    /// <para>
    /// The action is audited. Clients connected through this server will drop briefly.
    /// </para>
    /// </remarks>
    /// <returns>202 Accepted with a confirmation message.</returns>
    [HttpPost("restart")]
    public async Task<IActionResult> RestartServerAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Server restart requested by admin.");

        await _auditLogger.LogAsync(new AuditEntry
        {
            Caller = BuildCaller(),
            ModuleId = "dotnetcloud.core",
            Action = AuditAction.Update,
            EntityType = "Server",
            EntityId = Guid.CreateVersion7(),
            Description = "server-restart-requested",
        }, cancellationToken);

        // Fire-and-forget delayed graceful shutdown so the 202 response is flushed first.
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _logger.LogWarning("Restarting DotNetCloud per admin request.");
            _hostLifetime.StopApplication();
        }, cancellationToken);

        return Accepted(new { success = true, message = "Restart requested. The server will restart in a few seconds." });
    }

    /// <summary>
    /// Start a backup in the background and return immediately. Poll <c>backup/status</c> for
    /// progress and the final result. Use this for large datasets: a synchronous request is bounded
    /// by the client's HTTP timeout (100 seconds by default).
    /// </summary>
    /// <param name="outputPath">Optional explicit output path for the backup archive.</param>
    /// <returns>202 when the job started, 409 when a backup is already running.</returns>
    [HttpPost("backup/start")]
    public async Task<IActionResult> StartBackupAsync(
        [FromQuery] string? outputPath = null)
    {
        var settings = await _settingsService.ListSettingsAsync("dotnetcloud.core");
        var options = BuildBackupOptionsFromSettings(settings, outputPath);

        if (!_backupRunner.TryStart(options, outputPath, out var error))
        {
            return Conflict(new
            {
                success = false,
                error = new { code = "BACKUP_IN_PROGRESS", message = error },
            });
        }

        _logger.LogInformation("Background backup started by admin.");
        return Accepted(new { success = true, message = "Backup started." });
    }

    /// <summary>
    /// Request cancellation of the running background backup.
    /// </summary>
    /// <returns>200 when cancellation was requested, 409 when no backup is running.</returns>
    [HttpPost("backup/cancel")]
    public IActionResult CancelBackup()
    {
        if (!_backupRunner.TryCancel())
        {
            return Conflict(new
            {
                success = false,
                error = new { code = "NO_BACKUP_RUNNING", message = "No backup is running." },
            });
        }

        _logger.LogInformation("Background backup cancellation requested by admin.");
        return Ok(new { success = true, message = "Cancellation requested." });
    }

    /// <summary>
    /// Gets the current backup status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether a backup is running and info about the last backup.</returns>
    [HttpGet("backup/status")]
    public async Task<IActionResult> GetBackupStatusAsync(CancellationToken cancellationToken = default)
    {
        // Prefer the background runner while it is active or when it has the most recent result;
        // otherwise fall back to the service (covers synchronous and scheduled runs).
        var runnerStatus = _backupRunner.GetStatus();
        var serviceStatus = await _backupService.GetStatusAsync(cancellationToken);

        if (runnerStatus.IsRunning)
            return Ok(new { success = true, data = runnerStatus });

        if (runnerStatus.LastBackupTime is not null
            && (serviceStatus.LastBackupTime is null || runnerStatus.LastBackupTime >= serviceStatus.LastBackupTime))
            return Ok(new { success = true, data = runnerStatus });

        return Ok(new { success = true, data = serviceStatus });
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

        _logger.LogWarning("Restore from backup triggered by admin: {FilePath}", SanitizeForLog(filePath));

        var options = new RestoreOptions
        {
            RestoreDatabase = restoreDatabase,
            DataDirectory = Environment.GetEnvironmentVariable("DOTNETCLOUD_DATA_DIR")
        };
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
                case "Backup:Compression":
                    options.Compression = setting.Value;
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
            // The module host rejects unauthenticated data-plane calls; the provider's channel
            // carries the supervisor-issued credential.
            var channel = _channelProvider.GetOrCreateChannel(moduleId, endpoint);
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
