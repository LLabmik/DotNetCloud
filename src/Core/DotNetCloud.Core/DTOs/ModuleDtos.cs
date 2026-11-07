namespace DotNetCloud.Core.DTOs;

/// <summary>
/// Data transfer object for module information.
/// </summary>
public class ModuleDto
{
    /// <summary>
    /// Gets or sets the unique identifier for the module (e.g., "dotnetcloud.files").
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module name.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module version.
    /// </summary>
    public string Version { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this module is architecturally required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the module trust tier (<c>Required</c>, <c>Bundled</c> or <c>Store</c>).
    /// </summary>
    public string TrustTier { get; set; } = null!;

    /// <summary>
    /// Gets or sets the manifest data-store mode (<c>None</c> or <c>Isolated</c>).
    /// </summary>
    public string DataStoreMode { get; set; } = null!;

    /// <summary>
    /// Gets or sets the sandbox status (<c>Active</c>, <c>PendingProvisioning</c>, <c>Error</c>,
    /// <c>QuotaExceeded</c> or <c>LegacyOverride</c>).
    /// </summary>
    public string SandboxStatus { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module status (Enabled, Disabled, UpdateAvailable).
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date and time the module was installed.
    /// </summary>
    public DateTime InstalledAt { get; set; }

    /// <summary>
    /// Gets or sets the required capabilities for this module.
    /// </summary>
    public ICollection<string> RequiredCapabilities { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the events published by this module.
    /// </summary>
    public ICollection<string> PublishedEvents { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the events subscribed to by this module.
    /// </summary>
    public ICollection<string> SubscribedEvents { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the granted capabilities for this module.
    /// </summary>
    public ICollection<ModuleCapabilityGrantDto> GrantedCapabilities { get; set; } = new List<ModuleCapabilityGrantDto>();
}

/// <summary>
/// Data transfer object for creating/registering a module.
/// </summary>
public class CreateModuleDto
{
    /// <summary>
    /// Gets or sets the module ID (required, e.g., "dotnetcloud.files").
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module name (required).
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module version (required).
    /// </summary>
    public string Version { get; set; } = null!;

    /// <summary>
    /// Gets or sets the module description (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the required capabilities (optional).
    /// </summary>
    public ICollection<string> RequiredCapabilities { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the events published by this module (optional).
    /// </summary>
    public ICollection<string> PublishedEvents { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the events subscribed to by this module (optional).
    /// </summary>
    public ICollection<string> SubscribedEvents { get; set; } = new List<string>();
}

/// <summary>
/// Data transfer object describing the isolated data store of a module.
/// </summary>
/// <remarks>
/// The module's scoped connection string is never returned by this DTO; only whether the stored
/// credentials can still be decrypted with the current DataProtection key ring is exposed
/// (<see cref="CredentialsReadable"/>). A <see langword="false"/> value means the operator must run
/// the credential-reset recovery path.
/// </remarks>
public class ModuleDataStoreDto
{
    /// <summary>Gets or sets the module identifier.</summary>
    public string ModuleId { get; set; } = null!;

    /// <summary>Gets or sets the manifest data-store mode (<c>None</c> or <c>Isolated</c>).</summary>
    public string DataStoreMode { get; set; } = null!;

    /// <summary>Gets or sets whether a data-store row exists for the module.</summary>
    public bool Provisioned { get; set; }

    /// <summary>Gets or sets the database engine (<c>PostgreSQL</c> or <c>SqlServer</c>).</summary>
    public string? Provider { get; set; }

    /// <summary>Gets or sets the schema owned by the module principal.</summary>
    public string? SchemaName { get; set; }

    /// <summary>Gets or sets the module database principal name.</summary>
    public string? PrincipalName { get; set; }

    /// <summary>Gets or sets the provisioning status.</summary>
    public string Status { get; set; } = null!;

    /// <summary>Gets or sets the sanitized, admin-visible failure reason (never a secret).</summary>
    public string? LastError { get; set; }

    /// <summary>Gets or sets when the store was first provisioned.</summary>
    public DateTime? CreatedAtUtc { get; set; }

    /// <summary>Gets or sets when the store row last changed.</summary>
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Gets or sets whether the stored connection string can be decrypted.</summary>
    public bool CredentialsReadable { get; set; }

    /// <summary>Gets or sets the effective connection limit (quota override, otherwise the default).</summary>
    public int ConnectionLimit { get; set; }
}

/// <summary>
/// Data transfer object for the outcome of a data-store lifecycle operation.
/// </summary>
public class ModuleDataStoreOperationResultDto
{
    /// <summary>Gets or sets whether the operation completed.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the module schema, when the operation succeeded.</summary>
    public string? SchemaName { get; set; }

    /// <summary>Gets or sets the module principal, when the operation succeeded.</summary>
    public string? PrincipalName { get; set; }

    /// <summary>Gets or sets a sanitized failure reason; never contains a secret.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Data transfer object for per-module resource quotas (F10).
/// </summary>
/// <remarks>
/// The <c>MemoryLimitMb</c>/... properties are the admin override; <see langword="null"/> means
/// inherit. The <c>Effective*</c> properties are what the supervisor actually applies after
/// <c>min(manifest request, override, Modules:Quotas policy ceiling)</c>.
/// </remarks>
public class ModuleQuotaDto
{
    /// <summary>Gets or sets the module identifier.</summary>
    public string ModuleId { get; set; } = null!;

    /// <summary>Gets or sets the admin memory override in MB, or null to inherit.</summary>
    public int? MemoryLimitMb { get; set; }

    /// <summary>Gets or sets the admin CPU override (percent of one core), or null to inherit.</summary>
    public int? CpuPercent { get; set; }

    /// <summary>Gets or sets the admin data-directory override in MB, or null to inherit.</summary>
    public int? DataDirMb { get; set; }

    /// <summary>Gets or sets the admin database-size override in MB, or null to inherit.</summary>
    public int? DatabaseMb { get; set; }

    /// <summary>Gets or sets the admin connection-limit override, or null to inherit.</summary>
    public int? ConnectionLimit { get; set; }

    /// <summary>Gets or sets the admin RPC-rate override, or null to inherit.</summary>
    public int? RequestsPerMinute { get; set; }

    /// <summary>Gets or sets the admin event-rate override, or null to inherit.</summary>
    public int? EventsPerMinute { get; set; }

    /// <summary>Gets or sets the admin notification-rate override, or null to inherit.</summary>
    public int? NotificationsPerMinute { get; set; }

    /// <summary>Gets or sets the admin audit-rate override, or null to inherit.</summary>
    public int? AuditEntriesPerMinute { get; set; }

    /// <summary>Gets or sets when the override last changed.</summary>
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Gets or sets the administrator who last changed the override.</summary>
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>Gets or sets the effective memory ceiling in MB, or null for none.</summary>
    public int? EffectiveMemoryLimitMb { get; set; }

    /// <summary>Gets or sets the effective CPU share, or null for none.</summary>
    public int? EffectiveCpuPercent { get; set; }

    /// <summary>Gets or sets the effective task limit applied to every module cgroup.</summary>
    public int EffectivePidsLimit { get; set; }

    /// <summary>Gets or sets the effective data-directory ceiling in MB, or null when unmeasured.</summary>
    public int? EffectiveDataDirMb { get; set; }

    /// <summary>Gets or sets the effective database-size ceiling in MB, or null when unmeasured.</summary>
    public int? EffectiveDatabaseMb { get; set; }

    /// <summary>Gets or sets the effective database connection limit.</summary>
    public int EffectiveConnectionLimit { get; set; }

    /// <summary>Gets or sets the effective RPC rate, or null for none.</summary>
    public int? EffectiveRequestsPerMinute { get; set; }

    /// <summary>Gets or sets the effective event rate, or null for none.</summary>
    public int? EffectiveEventsPerMinute { get; set; }

    /// <summary>Gets or sets the effective notification rate, or null for none.</summary>
    public int? EffectiveNotificationsPerMinute { get; set; }

    /// <summary>Gets or sets the effective audit rate, or null for none.</summary>
    public int? EffectiveAuditEntriesPerMinute { get; set; }
}

/// <summary>
/// Data transfer object for updating a module's resource quota override.
/// </summary>
/// <remarks>A <see langword="null"/> or zero value clears the override and inherits policy.</remarks>
public class UpdateModuleQuotaDto
{
    /// <summary>Gets or sets the memory override in MB.</summary>
    public int? MemoryLimitMb { get; set; }

    /// <summary>Gets or sets the CPU override (percent of one core).</summary>
    public int? CpuPercent { get; set; }

    /// <summary>Gets or sets the data-directory override in MB.</summary>
    public int? DataDirMb { get; set; }

    /// <summary>Gets or sets the database-size override in MB.</summary>
    public int? DatabaseMb { get; set; }

    /// <summary>Gets or sets the connection-limit override.</summary>
    public int? ConnectionLimit { get; set; }

    /// <summary>Gets or sets the RPC-rate override.</summary>
    public int? RequestsPerMinute { get; set; }

    /// <summary>Gets or sets the event-rate override.</summary>
    public int? EventsPerMinute { get; set; }

    /// <summary>Gets or sets the notification-rate override.</summary>
    public int? NotificationsPerMinute { get; set; }

    /// <summary>Gets or sets the audit-rate override.</summary>
    public int? AuditEntriesPerMinute { get; set; }
}

/// <summary>
/// Data transfer object for the outcome of a quota update.
/// </summary>
public class ModuleQuotaOperationResultDto
{
    /// <summary>Gets or sets whether the quota was stored.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets a validation or persistence error.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets the stored quota and its effective values after a successful update.</summary>
    public ModuleQuotaDto? Quota { get; set; }
}

/// <summary>
/// Data transfer object for module capability grant information.
/// </summary>
public class ModuleCapabilityGrantDto
{
    /// <summary>
    /// Gets or sets the module ID.
    /// </summary>
    public string ModuleId { get; set; } = null!;

    /// <summary>
    /// Gets or sets the capability name.
    /// </summary>
    public string CapabilityName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the date and time the capability was granted.
    /// </summary>
    public DateTime GrantedAt { get; set; }

    /// <summary>
    /// Gets or sets the ID of the admin who granted this capability (if applicable).
    /// </summary>
    public Guid? GrantedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the admin who granted this capability (if applicable).
    /// </summary>
    public string? GrantedByUserDisplayName { get; set; }
}

/// <summary>
/// Data transfer object for granting a capability to a module.
/// </summary>
public class GrantModuleCapabilityDto
{
    /// <summary>
    /// Gets or sets the capability name to grant (required).
    /// </summary>
    public string CapabilityName { get; set; } = null!;
}
