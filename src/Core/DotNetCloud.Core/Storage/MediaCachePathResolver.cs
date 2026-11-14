using Microsoft.Extensions.Configuration;

namespace DotNetCloud.Core.Storage;

/// <summary>
/// Resolves the directories that content-addressed media assets (album art, posters, thumbnails)
/// are cached under.
/// </summary>
/// <remarks>
/// <para>
/// The cache must live somewhere both the core process (which renders Blazor module UIs) and the
/// owning module host (which serves the image endpoints) resolve to the same persistent path. Module
/// hosts do not receive <c>Files:StoragePath</c> in <c>config.json</c>, so a bare
/// <see cref="Path.GetTempPath"/> fallback silently splits the two processes: the UI writes the asset
/// into the module host's temp directory, it is lost on the next service restart, and the image
/// disappears.
/// </para>
/// <para>
/// Resolution order:
/// <list type="number">
/// <item><c>Files:Storage:MediaCachePath</c> (explicit override);</item>
/// <item><c>{Files:Storage:RootPath}/.media-cache</c>;</item>
/// <item><c>{DOTNETCLOUD_DATA_DIR}/storage/.media-cache</c> — the location <c>tools/install.sh</c> seeds
/// and the one a hardened systemd unit keeps writable;</item>
/// <item><c>{temp}/.media-cache</c> for standalone developer runs only.</item>
/// </list>
/// </para>
/// </remarks>
public static class MediaCachePathResolver
{
    /// <summary>Configuration key carrying the explicit media-cache directory.</summary>
    public const string MediaCachePathConfigKey = "Files:Storage:MediaCachePath";

    /// <summary>Configuration key carrying the platform-wide file storage root.</summary>
    public const string StorageRootConfigKey = "Files:Storage:RootPath";

    /// <summary>Environment variable that points at the persistent data directory.</summary>
    public const string DataDirEnvironmentVariable = "DOTNETCLOUD_DATA_DIR";

    /// <summary>Name of the media-cache directory inside the storage root.</summary>
    public const string MediaCacheFolderName = ".media-cache";

    /// <summary>Name of the storage directory inside the data directory.</summary>
    public const string StorageFolderName = "storage";

    /// <summary>
    /// Resolves the media-cache directory that content-addressed assets are written to and served from.
    /// </summary>
    /// <param name="configuration">Configuration to read the storage keys from.</param>
    /// <param name="dataDirectory">
    /// Value of <c>DOTNETCLOUD_DATA_DIR</c>, or <see langword="null"/> to read the environment.
    /// </param>
    /// <returns>The absolute media-cache directory.</returns>
    public static string ResolveMediaCachePath(IConfiguration configuration, string? dataDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration[MediaCachePathConfigKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return Path.Combine(ResolveStorageRoot(configuration, dataDirectory), MediaCacheFolderName);
    }

    /// <summary>
    /// Resolves the file storage root (the parent of <c>.media-cache</c>, <c>.album-art</c>, <c>.video-posters</c>, …).
    /// </summary>
    /// <param name="configuration">Configuration to read the storage keys from.</param>
    /// <param name="dataDirectory">
    /// Value of <c>DOTNETCLOUD_DATA_DIR</c>, or <see langword="null"/> to read the environment.
    /// </param>
    /// <returns>The absolute storage root.</returns>
    public static string ResolveStorageRoot(IConfiguration configuration, string? dataDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configured = configuration[StorageRootConfigKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        dataDirectory ??= Environment.GetEnvironmentVariable(DataDirEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(dataDirectory))
        {
            return Path.Combine(dataDirectory, StorageFolderName);
        }

        return Path.GetTempPath();
    }
}
