namespace DotNetCloud.Modules.Files.Options;

/// <summary>
/// Configuration for admin-managed shared folders exposed through the Files module.
/// </summary>
public sealed class AdminSharedFolderOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Files:AdminSharedFolders";

    /// <summary>
    /// Canonical host root beneath which admins may register shared folders.
    /// Relative source paths are resolved against this root.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;
}