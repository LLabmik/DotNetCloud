namespace DotNetCloud.Core.Data.Entities.Modules;

/// <summary>
/// Effective resource quota for a module.
/// </summary>
/// <remarks>
/// <para>
/// The manifest's <c>resources</c> block is a <em>request</em>, never an entitlement: the effective
/// limit is <c>min(manifest request, admin policy, Modules:Quotas:Max*)</c>. A null value means
/// "inherit the configured default".
/// </para>
/// <para>
/// Kernel-enforced limits (memory, CPU, process count, open files) are applied by the process
/// supervisor's <c>ResourceLimiter</c>; app-level limits are enforced per module at the API.
/// Database and data-directory sizes are measured and enforced by suspension.
/// </para>
/// </remarks>
public class ModuleQuota
{
    /// <summary>Primary key and quota owner.</summary>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Memory ceiling in megabytes (cgroup <c>memory.max</c>).</summary>
    public int? MemoryLimitMb { get; set; }

    /// <summary>CPU ceiling as a percentage of one core (cgroup <c>cpu.max</c>).</summary>
    public int? CpuPercent { get; set; }

    /// <summary>Maximum size of the module's data directory, in megabytes.</summary>
    public int? DataDirMb { get; set; }

    /// <summary>Maximum size of the module's database schema, in megabytes.</summary>
    public int? DatabaseMb { get; set; }

    /// <summary>Maximum concurrent database connections (PostgreSQL <c>CONNECTION LIMIT</c> / pool size).</summary>
    public int? ConnectionLimit { get; set; }

    /// <summary>Maximum capability RPCs per minute.</summary>
    public int? RequestsPerMinute { get; set; }

    /// <summary>Maximum published events per minute.</summary>
    public int? EventsPerMinute { get; set; }

    /// <summary>Maximum notifications per minute.</summary>
    public int? NotificationsPerMinute { get; set; }

    /// <summary>Maximum audit entries per minute.</summary>
    public int? AuditEntriesPerMinute { get; set; }

    /// <summary>When the quota last changed.</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>The administrator who last changed the quota.</summary>
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>Navigation to the owning module.</summary>
    public virtual InstalledModule? Module { get; set; }
}
