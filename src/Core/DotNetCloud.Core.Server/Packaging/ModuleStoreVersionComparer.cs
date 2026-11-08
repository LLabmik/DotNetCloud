using DotNetCloud.Core.Server.ModuleLoading;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// Orders module version strings with the core's own semver rules.
/// </summary>
/// <remarks>
/// A store index is JSON, so versions arrive as strings. Ordering them with
/// <see cref="StringComparer.Ordinal"/> would put <c>1.10.0</c> before <c>1.9.0</c> and make
/// "latest" wrong; every store source therefore shares this comparer.
/// </remarks>
internal sealed class ModuleStoreVersionComparer : IComparer<string>
{
    /// <summary>The shared instance.</summary>
    public static readonly ModuleStoreVersionComparer Instance = new();

    /// <inheritdoc/>
    public int Compare(string? x, string? y)
    {
        var hasX = ModuleCompatibility.TryParseVersion(x, out var left);
        var hasY = ModuleCompatibility.TryParseVersion(y, out var right);

        return (hasX, hasY) switch
        {
            (true, true) => left.CompareTo(right),
            (true, false) => 1,
            (false, true) => -1,
            _ => string.CompareOrdinal(x, y),
        };
    }
}
