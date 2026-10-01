using DotNetCloud.Modules.Files.DTOs;

namespace DotNetCloud.Modules.Files;

/// <summary>
/// Well-known identifiers for the synthetic (virtual) nodes surfaced by the Files module.
/// Single source of truth for display names and <see cref="FileNodeDto.VirtualSourceKind"/> values.
/// </summary>
public static class VirtualNodeSourceKinds
{
    /// <summary>Display name of the per-user virtual root that hosts admin shared folders.</summary>
    public const string DotNetCloudRootDisplayName = "_DotNetCloud";

    /// <summary>
    /// Source discriminator (<see cref="FileNodeDto.VirtualSourceKind"/>) of the
    /// <c>_DotNetCloud</c> virtual root node.
    /// </summary>
    public const string DotNetCloudRoot = "DotNetCloudRoot";

    /// <summary>
    /// Material Icons ligature name used for the <c>_DotNetCloud</c> virtual root, so it reads as a
    /// cloud-backed root rather than an ordinary folder.
    /// </summary>
    public const string DotNetCloudRootIcon = "cloud";

    /// <summary>
    /// Returns whether a node is the <c>_DotNetCloud</c> virtual root, matched by either its
    /// source discriminator or its display name.
    /// </summary>
    /// <param name="name">Node display name.</param>
    /// <param name="virtualSourceKind">Synthetic source kind, when the node is a virtual node.</param>
    /// <returns><see langword="true"/> when the node is the <c>_DotNetCloud</c> virtual root.</returns>
    public static bool IsDotNetCloudRoot(string? name, string? virtualSourceKind)
        => string.Equals(virtualSourceKind, DotNetCloudRoot, StringComparison.Ordinal)
            || string.Equals(name, DotNetCloudRootDisplayName, StringComparison.Ordinal);
}
