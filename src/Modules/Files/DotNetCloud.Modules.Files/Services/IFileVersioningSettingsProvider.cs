using DotNetCloud.Modules.Files.Options;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Resolves the effective file versioning policy: the configuration-bound
/// <see cref="VersionRetentionOptions"/> with the administrator's <c>/admin/files</c> edits
/// layered on top.
/// </summary>
/// <remarks>
/// Configured values come from configuration (<c>Files:VersionRetention</c>, usually
/// <c>/etc/dotnetcloud/env</c>), while the admin page stores its edits as core system settings
/// (module <c>dotnetcloud.files</c>, keys <c>VersionRetention:*</c>). Version creation, the retention
/// sweep and the admin readout all resolve their policy here so the page is authoritative; the
/// implementation lives in <c>DotNetCloud.Modules.Files.Data</c>.
/// </remarks>
public interface IFileVersioningSettingsProvider
{
    /// <summary>Effective options, refreshed when the cache window has elapsed.</summary>
    VersionRetentionOptions Current { get; }

    /// <summary>Effective options, refreshing the cache when it has elapsed.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<VersionRetentionOptions> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the cached values so the next read hits the database.</summary>
    void Invalidate();
}
