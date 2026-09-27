using DotNetCloud.Modules.Files.Options;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Resolves the effective <see cref="CollaboraOptions"/>: the configuration-bound values with the
/// administrator's <c>/admin/collabora</c> edits layered on top.
/// </summary>
/// <remarks>
/// Configured Collabora settings come from configuration (<c>Files:Collabora:*</c>, usually
/// <c>/etc/dotnetcloud/env</c>), while the admin page stores its edits as core system settings
/// (module <c>dotnetcloud.files</c>, keys <c>Collabora:*</c>). Consumers resolve their options here
/// so the page is authoritative; the implementation lives in
/// <c>DotNetCloud.Modules.Files.Data</c>.
/// </remarks>
public interface ICollaboraSettingsProvider
{
    /// <summary>Effective options, refreshed when the cache window has elapsed.</summary>
    CollaboraOptions Current { get; }

    /// <summary>Effective options, refreshing the cache when it has elapsed.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CollaboraOptions> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the cached values so the next read hits the database.</summary>
    void Invalidate();
}
