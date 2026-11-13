using System.Reflection;

namespace DotNetCloud.Core.Server.ModuleLoading;

/// <summary>
/// Core-version compatibility rules for module manifests.
/// </summary>
/// <remarks>
/// <para>
/// <c>minCoreVersion</c> used to be parsed and then ignored, so an incompatible module started
/// and failed at runtime instead of being refused with a reason an administrator can act on.
/// </para>
/// <para>
/// Only the numeric <c>major.minor.patch</c> part participates in the comparison; a pre-release
/// suffix on either side is ignored for the minimum-version check.
/// </para>
/// </remarks>
internal static class ModuleCompatibility
{
    /// <summary>Gets the running core product version (for example <c>0.7.0</c>).</summary>
    public static Version CoreVersion { get; } = ResolveCoreVersion();

    /// <summary>
    /// Determines whether a module manifest is compatible with the running core.
    /// </summary>
    /// <param name="manifest">The manifest to check.</param>
    /// <param name="reason">
    /// When the manifest is incompatible, a human-readable explanation; otherwise
    /// <see langword="null"/>.
    /// </param>
    /// <returns><see langword="true"/> when the module may start.</returns>
    public static bool TryGetIncompatibilityReason(ModuleManifestData manifest, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        reason = null;

        if (string.IsNullOrWhiteSpace(manifest.MinCoreVersion))
        {
            return false;
        }

        if (!TryParseVersion(manifest.MinCoreVersion, out var minimum))
        {
            reason =
                $"manifest declares an unparsable minCoreVersion '{manifest.MinCoreVersion}'";
            return true;
        }

        if (CoreVersion < minimum)
        {
            reason =
                $"requires core >= {minimum} but this core is {CoreVersion}";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses a semantic version's numeric part (a leading <c>v</c>, a pre-release suffix and build
    /// metadata are ignored).
    /// </summary>
    /// <param name="value">The version text to parse.</param>
    /// <param name="version">The normalized three-component version.</param>
    /// <returns><see langword="true"/> when the text contains a parseable version.</returns>
    public static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();

        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        var metadataIndex = text.IndexOf('+');
        if (metadataIndex >= 0)
        {
            text = text[..metadataIndex];
        }

        var preReleaseIndex = text.IndexOf('-');
        if (preReleaseIndex >= 0)
        {
            text = text[..preReleaseIndex];
        }

        if (!Version.TryParse(text, out var parsed))
        {
            return false;
        }

        version = Normalize(parsed);
        return true;
    }

    private static Version Normalize(Version version) =>
        new(
            Math.Max(version.Major, 0),
            Math.Max(version.Minor, 0),
            Math.Max(version.Build, 0));

    private static Version ResolveCoreVersion()
    {
        var assembly = typeof(ModuleCompatibility).Assembly;

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (TryParseVersion(informational, out var informationalVersion))
        {
            return informationalVersion;
        }

        return Normalize(assembly.GetName().Version ?? new Version(0, 0, 0));
    }
}
