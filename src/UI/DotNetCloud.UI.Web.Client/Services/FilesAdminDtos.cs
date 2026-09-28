namespace DotNetCloud.UI.Web.Client.Services;

/// <summary>
/// The <b>effective</b> file versioning policy currently enforced: the server-configured
/// <c>Files:VersionRetention</c> values with any administrator edits layered on top.
/// Served by <c>GET /api/v1/files/admin/versioning/effective</c>.
/// </summary>
public sealed record FileVersioningEffectiveSettingsDto
{
    /// <summary>Whether version history is recorded at all.</summary>
    public bool Enabled { get; init; }

    /// <summary>Configured maximum number of versions kept per file (<c>0</c> = no count limit).</summary>
    public int MaxNumber { get; init; }

    /// <summary>Configured maximum age in days of a retained version (<c>0</c> = no age limit).</summary>
    public int MaxDays { get; init; }

    /// <summary>
    /// The number of versions actually retained: <see cref="MaxNumber"/>, or <c>1</c> while
    /// versioning is switched off.
    /// </summary>
    public int EffectiveMaxNumber { get; init; }

    /// <summary>Whether the count limit is effectively unlimited.</summary>
    public bool UnlimitedVersions { get; init; }

    /// <summary>How often the scheduled retention pass runs, in hours.</summary>
    public double CleanupIntervalHours { get; init; }
}

/// <summary>
/// Outcome of running the version retention pass on demand, served by
/// <c>POST /api/v1/files/admin/versioning/prune</c>.
/// </summary>
public sealed record FileVersionPruneResultDto
{
    /// <summary>Whether version history is being recorded.</summary>
    public bool VersioningEnabled { get; init; }

    /// <summary>Number of files that had version history to consider.</summary>
    public int FilesScanned { get; init; }

    /// <summary>Number of versions removed by the pass.</summary>
    public int VersionsDeleted { get; init; }
}
