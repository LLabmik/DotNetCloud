namespace DotNetCloud.Modules.Files.Models;

/// <summary>
/// The type of entity a file or folder is shared with.
/// </summary>
public enum ShareType
{
    /// <summary>Shared with a specific user.</summary>
    User = 0,

    /// <summary>Shared with an entire team.</summary>
    Team = 1,

    /// <summary>Shared with a cross-team group.</summary>
    Group = 2,

    /// <summary>Shared via a public link (optionally password-protected).</summary>
    PublicLink = 3
}
