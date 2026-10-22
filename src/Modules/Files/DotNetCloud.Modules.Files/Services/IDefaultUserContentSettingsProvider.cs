using DotNetCloud.Modules.Files.Options;

namespace DotNetCloud.Modules.Files.Services;

/// <summary>
/// Provides the effective <see cref="DefaultUserContentOptions"/>: the configuration-bound
/// baseline with the administrator-editable values from <c>/admin/files</c> layered on top.
/// </summary>
public interface IDefaultUserContentSettingsProvider
{
    /// <summary>Effective options, refreshed when the cache window has elapsed.</summary>
    DefaultUserContentOptions Current { get; }

    /// <summary>Effective options, refreshing the cache when it has elapsed.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DefaultUserContentOptions> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the cached values so the next read hits the database.</summary>
    void Invalidate();
}
