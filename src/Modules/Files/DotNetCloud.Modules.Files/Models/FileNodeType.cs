namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Distinguishes between file, folder, and symbolic link nodes in the file system tree.
/// </summary>
public enum FileNodeType
{
    /// <summary>A regular file with content.</summary>
    File = 0,

    /// <summary>A folder that can contain child nodes.</summary>
    Folder = 1,

    /// <summary>
    /// A symbolic link. The link target is stored in <see cref="FileNode.LinkTarget"/>.
    /// Only relative targets within the sync root are accepted; absolute targets and
    /// targets that escape the sync root are rejected at upload time by the client.
    /// </summary>
    SymbolicLink = 2
}
