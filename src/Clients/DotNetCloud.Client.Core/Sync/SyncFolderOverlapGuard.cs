namespace DotNetCloud.Client.Core.Sync;

/// <summary>
/// Detects whether two local sync folder paths overlap (one contains the other, or they are
/// equal). Used to prevent registering nested or duplicate sync folders in either direction.
/// </summary>
public static class SyncFolderOverlapGuard
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="a"/> and <paramref name="b"/> are the same
    /// folder or one is contained within the other (after full-path normalization).
    /// Path comparison is case-sensitive on non-Windows platforms and case-insensitive on Windows.
    /// </summary>
    public static bool PathsOverlap(string a, string b)
    {
        var fullA = Path.GetFullPath(a);
        var fullB = Path.GetFullPath(b);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return IsWithin(fullA, fullB, comparison) || IsWithin(fullB, fullA, comparison);
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="candidate"/> equals <paramref name="root"/> or
    /// is a descendant of it (i.e. starts with <paramref name="root"/> plus a directory separator).
    /// </summary>
    private static bool IsWithin(string root, string candidate, StringComparison comparison)
    {
        var normalizedRoot = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return string.Equals(candidate, root, comparison)
            || candidate.StartsWith(normalizedRoot, comparison);
    }
}
