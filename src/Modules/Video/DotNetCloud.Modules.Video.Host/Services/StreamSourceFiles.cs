namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Decides whether a file handed to the video streaming endpoints is a scratch copy this module owns
/// and is therefore allowed to delete.
/// </summary>
/// <remarks>
/// A download can legitimately hand back a direct file stream over <b>permanent</b> storage: whole-file
/// media blobs (<c>FileVersion.IsChunked = 0</c>, served straight out of the storage root) and files in
/// admin-shared folders. The probe endpoints (<c>/{id}/streams</c>, <c>/{id}/stream-probe</c>,
/// <c>/{id}/stream/seek</c>) clean up whatever path they were given, so an unguarded delete destroys the
/// user's content — the blob then reads back as "missing from storage" and the video can never be played
/// again. Only files that live inside the system temp directory are safe to remove.
/// </remarks>
public static class StreamSourceFiles
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="path"/> is a scratch file inside the system
    /// temp directory, i.e. one the streaming endpoints are allowed to delete.
    /// </summary>
    /// <param name="path">Candidate file path.</param>
    public static bool IsDeletableScratchFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string fullPath;
        string tempRoot;
        try
        {
            fullPath = Path.GetFullPath(path);
            tempRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return fullPath.StartsWith(tempRoot, StringComparison.Ordinal);
    }
}
