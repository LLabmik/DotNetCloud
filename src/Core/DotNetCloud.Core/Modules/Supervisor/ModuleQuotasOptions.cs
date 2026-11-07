namespace DotNetCloud.Core.Modules.Supervisor;

/// <summary>
/// Policy ceilings and defaults for per-module resource quotas (F10).
/// </summary>
/// <remarks>
/// <para>
/// A module manifest's <c>resources</c> block is a <em>request</em>, never an entitlement. The
/// effective limit is <c>min(manifest request, admin override, policy ceiling)</c>; a null ceiling
/// means the dimension is unbounded by policy.
/// </para>
/// <para>Bind from <c>appsettings.json</c> section <c>"Modules:Quotas"</c>.</para>
/// </remarks>
public sealed class ModuleQuotasOptions
{
    /// <summary>The configuration section name in appsettings.json.</summary>
    public const string SectionName = "Modules:Quotas";

    /// <summary>Gets or sets the maximum memory ceiling (MB) any module may receive. Null means unbounded.</summary>
    public int? MaxMemoryLimitMb { get; set; } = 4096;

    /// <summary>Gets or sets the maximum CPU share (percent of one core) any module may receive. Null means unbounded.</summary>
    public int? MaxCpuPercent { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of tasks (threads/processes) each module cgroup may create.
    /// Applied to every module; there is no per-module override for this dimension.
    /// </summary>
    public int MaxPids { get; set; } = 1024;

    /// <summary>Gets or sets the maximum data-directory ceiling (MB) any module may receive. Null means unbounded.</summary>
    public int? MaxDataDirMb { get; set; }

    /// <summary>Gets or sets the maximum database-schema ceiling (MB) any module may receive. Null means unbounded.</summary>
    public int? MaxDatabaseMb { get; set; }

    /// <summary>Gets or sets the maximum database connection limit any module may receive.</summary>
    public int MaxConnectionLimit { get; set; } = 20;

    /// <summary>Gets or sets the default database connection limit when neither the manifest nor an override sets one.</summary>
    public int DefaultConnectionLimit { get; set; } = 10;

    /// <summary>Gets or sets the maximum capability RPCs per minute any module may receive. Null means unbounded.</summary>
    public int? MaxRequestsPerMinute { get; set; } = 6000;

    /// <summary>Gets or sets the maximum published events per minute any module may receive. Null means unbounded.</summary>
    public int? MaxEventsPerMinute { get; set; } = 600;

    /// <summary>Gets or sets the maximum notifications per minute any module may receive. Null means unbounded.</summary>
    public int? MaxNotificationsPerMinute { get; set; } = 120;

    /// <summary>Gets or sets the maximum audit entries per minute any module may write. Null means unbounded.</summary>
    public int? MaxAuditEntriesPerMinute { get; set; } = 600;

    /// <summary>
    /// Gets or sets how often the usage monitor measures database and data-directory sizes, in
    /// minutes. The default is 5; values below 1 are clamped to 1.
    /// </summary>
    public int UsageCheckIntervalMinutes { get; set; } = 5;
}
