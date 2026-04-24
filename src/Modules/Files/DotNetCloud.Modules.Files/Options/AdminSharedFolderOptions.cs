namespace DotNetCloud.Modules.Files.Options;

/// <summary>
/// Configuration for admin-managed shared folders exposed through the Files module.
/// </summary>
public sealed class AdminSharedFolderOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Files:AdminSharedFolders";

    /// <summary>
    /// Legacy host root setting retained for compatibility.
    /// Admin shared-folder browsing and validation now use the platform filesystem root.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;
}