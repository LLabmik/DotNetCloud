namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Distinguishes between file and folder nodes in the file system tree.
/// </summary>
public enum FileNodeType
{
    /// <summary>A regular file with content.</summary>
    File = 0,

    /// <summary>A folder that can contain child nodes.</summary>
    Folder = 1
}
