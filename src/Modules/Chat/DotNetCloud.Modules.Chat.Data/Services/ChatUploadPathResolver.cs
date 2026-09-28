using Microsoft.Extensions.Configuration;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Resolves the root directory that chat image uploads are written under.
/// </summary>
/// <remarks>
/// Writes must never land in the deploy directory: the service runs under a hardened systemd unit
/// (<c>ProtectSystem=strict</c>), so <c>/opt/dotnetcloud</c> is read-only. The previous
/// "current directory + storage" fallback therefore threw
/// <see cref="IOException"/> ("Read-only file system") from the constructor, which failed
/// <see cref="LocalChatImageStore"/> activation and surfaced as HTTP 500 from every Chat REST
/// endpoint (including the admin effective-policy readout).
/// </remarks>
internal static class ChatUploadPathResolver
{
    /// <summary>Configuration key that carries the platform-wide file storage root.</summary>
    internal const string StorageRootConfigKey = "Files:Storage:RootPath";

    /// <summary>Environment variable that points at the persistent data directory.</summary>
    internal const string DataDirEnvironmentVariable = "DOTNETCLOUD_DATA_DIR";

    /// <summary>Name of the subdirectory of the storage root that holds chat uploads.</summary>
    internal const string UploadsFolderName = "chat-uploads";

    /// <summary>URL prefix that served chat uploads use.</summary>
    internal const string UploadUrlPrefix = "/api/v1/chat/uploads/";

    /// <summary>
    /// Resolves the storage root, preferring (in order):
    /// <list type="number">
    /// <item>the configured <c>Files:Storage:RootPath</c>;</item>
    /// <item><c>{dataRoot}/storage</c>, where <c>dataRoot</c> is <c>DOTNETCLOUD_DATA_DIR</c>
    /// (the installer seeds its <c>chat-uploads</c> subdirectory);</item>
    /// <item><c>{current directory}/storage</c>, for standalone developer runs.</item>
    /// </list>
    /// </summary>
    /// <param name="configuration">Configuration to read <c>Files:Storage:RootPath</c> from.</param>
    /// <param name="dataRoot">Value of <c>DOTNETCLOUD_DATA_DIR</c>, or <see langword="null"/> when unset.</param>
    /// <returns>The directory that <c>chat-uploads</c> is created under.</returns>
    internal static string Resolve(IConfiguration configuration, string? dataRoot)
    {
        var storagePath = configuration.GetValue<string>(StorageRootConfigKey);
        if (!string.IsNullOrWhiteSpace(storagePath))
        {
            return storagePath;
        }

        if (!string.IsNullOrWhiteSpace(dataRoot))
        {
            return Path.Combine(dataRoot, "storage");
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "storage");
    }

    /// <summary>
    /// Resolves the directory chat-upload files live in (<c>{storage root}/chat-uploads</c>).
    /// </summary>
    /// <param name="configuration">Configuration to read <c>Files:Storage:RootPath</c> from.</param>
    /// <param name="dataRoot">Value of <c>DOTNETCLOUD_DATA_DIR</c>, or <see langword="null"/> when unset.</param>
    internal static string ResolveUploadsDirectory(IConfiguration configuration, string? dataRoot)
        => Path.Combine(Resolve(configuration, dataRoot), UploadsFolderName);
}
