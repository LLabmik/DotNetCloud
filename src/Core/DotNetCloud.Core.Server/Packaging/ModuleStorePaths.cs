namespace DotNetCloud.ModulePackaging;

/// <summary>
/// Containment and path-segment rules shared by every store source and the package installer.
/// </summary>
/// <remarks>
/// A store index is data, not authority: every path it names is resolved and then checked to be
/// inside the directory that owns it. The guard is a relative-path check with an explicit
/// <c>..</c> first-segment test rather than <c>StartsWith</c>, which is both weaker (prefix
/// collisions) and what static analysis rejects.
/// </remarks>
internal static class ModuleStorePaths
{
    /// <summary>The characters that may never appear in a cache path segment.</summary>
    private static readonly char[] InvalidSegmentCharacters =
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, ':', '\0', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// Whether <paramref name="candidate"/> resolves inside <paramref name="root"/>.
    /// </summary>
    /// <param name="root">The directory that owns the path.</param>
    /// <param name="candidate">The absolute path being checked.</param>
    /// <returns><see langword="true"/> when the path stays inside the root.</returns>
    public static bool IsInside(string root, string candidate)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(candidate));
        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        var firstSegment = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])[0];
        return !string.Equals(relative, ".", StringComparison.Ordinal)
               && !string.Equals(firstSegment, "..", StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether a value can be used as a single directory or file name without escaping its parent.
    /// </summary>
    /// <param name="segment">The candidate segment (a module id or a version).</param>
    /// <returns><see langword="true"/> when the segment is a plain name.</returns>
    public static bool IsSafeSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment)
            || segment.Length > 200
            || segment.IndexOfAny(InvalidSegmentCharacters) >= 0)
        {
            return false;
        }

        return !string.Equals(segment, ".", StringComparison.Ordinal)
               && !string.Equals(segment, "..", StringComparison.Ordinal);
    }

    /// <summary>
    /// Combines <paramref name="segments"/> under <paramref name="root"/> and refuses a result that
    /// escapes it.
    /// </summary>
    /// <param name="root">The owning directory.</param>
    /// <param name="segments">The path segments, each validated with <see cref="IsSafeSegment"/>.</param>
    /// <returns>The absolute contained path.</returns>
    /// <exception cref="ModulePackageException">A segment is unsafe or the result escapes the root.</exception>
    public static string CombineUnder(string root, params string[] segments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(segments);

        var current = Path.GetFullPath(root);

        foreach (var segment in segments)
        {
            if (!IsSafeSegment(segment))
            {
                throw new ModulePackageException(
                    $"'{segment}' is not a usable store path segment.");
            }

            current = Path.Combine(current, segment);
        }

        if (!IsInside(root, current))
        {
            throw new ModulePackageException(
                $"'{string.Join('/', segments)}' resolves outside '{root}'.");
        }

        return current;
    }
}
