namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Resolves the filesystem directory that expired chat messages are exported to before their
/// rows are deleted (retention mode <c>Archive</c>).
/// </summary>
/// <remarks>
/// The directory must be writable by the module host, which runs with systemd
/// <c>ProtectSystem=strict</c> — so the default lives under <c>DOTNETCLOUD_DATA_DIR</c> and never
/// under the read-only deploy directory. See <see cref="ChatUploadPathResolver"/> for the same
/// reasoning applied to uploads.
/// </remarks>
internal static class ChatArchivePathResolver
{
    /// <summary>Folder created under the data directory when no explicit path is configured.</summary>
    internal const string DefaultFolderName = "chat-archive";

    /// <summary>
    /// Resolves the archive root: the administrator-configured path when present, otherwise
    /// <c>{dataRoot}/storage/chat-archive</c>, otherwise
    /// <c>{current directory}/storage/chat-archive</c> for standalone developer runs.
    /// </summary>
    /// <param name="configuredPath">Value of the <c>Retention:ArchivePath</c> setting, if any.</param>
    /// <param name="dataRoot">Value of <c>DOTNETCLOUD_DATA_DIR</c>, or <see langword="null"/> when unset.</param>
    internal static string Resolve(string? configuredPath, string? dataRoot)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dataRoot))
        {
            return Path.Combine(dataRoot, "storage", DefaultFolderName);
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "storage", DefaultFolderName);
    }
}
