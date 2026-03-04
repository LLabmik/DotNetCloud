namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// Permission levels for file and folder shares.
/// </summary>
public enum SharePermission
{
    /// <summary>Read-only access: view and download.</summary>
    Read = 0,

    /// <summary>Read and write access: view, download, upload, and modify.</summary>
    ReadWrite = 1,

    /// <summary>Full access: all of ReadWrite plus delete and re-share.</summary>
    Full = 2
}
