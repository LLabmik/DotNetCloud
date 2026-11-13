namespace DotNetCloud.Core.Server.Supervisor;

/// <summary>
/// Filesystem containment checks that are safe against path traversal and sibling-prefix tricks.
/// </summary>
/// <remarks>
/// <para>
/// A prefix test such as <c>fullPath.StartsWith(root)</c> accepts <c>/data/modules-evil/x</c> for
/// the root <c>/data/modules</c>. CodeQL flags that pattern (path injection) and it is a real
/// escape: the supervisor would spawn a binary outside the modules directory.
/// </para>
/// <para>
/// The safe form is <see cref="Path.GetFullPath(string)"/> on both paths followed by
/// <see cref="Path.GetRelativePath(string, string)"/> and a check that the relative path does not
/// escape upwards. This mirrors the guard already used by <c>ModuleDiscoveryService</c>.
/// </para>
/// </remarks>
internal static class PathContainment
{
    /// <summary>
    /// Resolves <paramref name="candidatePath"/> and verifies it stays inside
    /// <paramref name="rootDirectory"/>.
    /// </summary>
    /// <param name="rootDirectory">The directory the candidate must stay within.</param>
    /// <param name="candidatePath">The path to resolve and check.</param>
    /// <param name="resolvedPath">
    /// The canonical absolute path when the check succeeds; otherwise an empty string.
    /// </param>
    /// <returns><see langword="true"/> when the candidate is inside the root.</returns>
    public static bool TryResolveWithin(string rootDirectory, string candidatePath, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        string root;
        string candidate;

        try
        {
            root = Path.GetFullPath(rootDirectory);
            candidate = Path.GetFullPath(candidatePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Malformed path: treat as uncontained rather than risking an unvalidated spawn.
            return false;
        }

        var relativePath = Path.GetRelativePath(root, candidate);

        if (Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return false;
        }

        resolvedPath = candidate;
        return true;
    }
}
