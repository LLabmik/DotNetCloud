namespace DotNetCloud.Core.Modules.Store;

/// <summary>
/// How much the configured module store vouches for an installable package (phase B5).
/// </summary>
/// <remarks>
/// <para>
/// This is <b>store</b> trust, not install provenance. The <c>ModuleTrustTier</c> column on
/// <c>InstalledModule</c> records how a module got onto the server (<c>Required</c> / <c>Bundled</c> /
/// <c>Store</c>) and decides whether capabilities are auto-granted. This enum is the finer
/// classification the catalogue publishes for a package version, and it only informs the
/// administrator.
/// </para>
/// <para>
/// The tier is derived by the store from its publisher registry and carried in the signed catalogue.
/// The client never infers it from the module id, the publisher name or the signature alone.
/// </para>
/// </remarks>
public enum ModuleStoreTrustTier
{
    /// <summary>The source did not declare a tier this client understands.</summary>
    Unknown = 0,

    /// <summary>Signed by the DotNetCloud project's own publisher key.</summary>
    Official,

    /// <summary>Signed by a publisher whose identity the store has verified.</summary>
    VerifiedPublisher,

    /// <summary>Signed by a registered publisher and published after review, without identity vetting.</summary>
    Community,

    /// <summary>
    /// Not backed by a key this server trusts: sideloaded with an explicit override, or fetched from a
    /// source that carries no curation at all.
    /// </summary>
    Unverified,
}

/// <summary>
/// Wire values and display names for <see cref="ModuleStoreTrustTier"/>.
/// </summary>
/// <remarks>
/// The wire values are the catalogue's own vocabulary (<c>official</c>, <c>verified-publisher</c>,
/// <c>community</c>) so one string is stored, transmitted and compared end to end. They match
/// <c>schemas/index.schema.json</c> in the store repository.
/// </remarks>
public static class ModuleStoreTrustTiers
{
    /// <summary>The DotNetCloud project's tier.</summary>
    public const string Official = "official";

    /// <summary>A vetted publisher's tier.</summary>
    public const string VerifiedPublisher = "verified-publisher";

    /// <summary>A registered-but-unvetted publisher's tier.</summary>
    public const string Community = "community";

    /// <summary>The tier of a package no trusted key signed.</summary>
    public const string Unverified = "unverified";

    /// <summary>
    /// Parses a catalogue value.
    /// </summary>
    /// <param name="value">The catalogue value, or <see langword="null"/>.</param>
    /// <returns>The tier; <see cref="ModuleStoreTrustTier.Unknown"/> for a missing or unrecognised value.</returns>
    public static ModuleStoreTrustTier Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Official => ModuleStoreTrustTier.Official,
        VerifiedPublisher => ModuleStoreTrustTier.VerifiedPublisher,
        Community => ModuleStoreTrustTier.Community,
        Unverified => ModuleStoreTrustTier.Unverified,
        _ => ModuleStoreTrustTier.Unknown,
    };

    /// <summary>
    /// Formats a tier as its catalogue value.
    /// </summary>
    /// <param name="tier">The tier.</param>
    /// <returns>The lower-case catalogue value, or <c>unknown</c>.</returns>
    public static string ToWireValue(this ModuleStoreTrustTier tier) => tier switch
    {
        ModuleStoreTrustTier.Official => Official,
        ModuleStoreTrustTier.VerifiedPublisher => VerifiedPublisher,
        ModuleStoreTrustTier.Community => Community,
        ModuleStoreTrustTier.Unverified => Unverified,
        _ => "unknown",
    };

    /// <summary>
    /// Formats a tier for an operator-facing label.
    /// </summary>
    /// <param name="tier">The tier.</param>
    /// <returns>A short human-readable label.</returns>
    public static string ToDisplayName(this ModuleStoreTrustTier tier) => tier switch
    {
        ModuleStoreTrustTier.Official => "Official",
        ModuleStoreTrustTier.VerifiedPublisher => "Verified publisher",
        ModuleStoreTrustTier.Community => "Community",
        ModuleStoreTrustTier.Unverified => "Unverified",
        _ => "Unspecified",
    };
}
