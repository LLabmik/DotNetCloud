namespace DotNetCloud.ModulePackaging;

/// <summary>
/// Configuration for the module store client and the package installer, bound from the
/// <c>Modules:Store</c> section.
/// </summary>
/// <remarks>
/// <para>
/// The first release installs from a <b>local feed directory</b> or an explicit <c>.dncpkg</c> file
/// (decision D6). <see cref="LocalFeedPath"/> and <see cref="TrustedKeys"/> are the whole trust
/// configuration: a package is only installed without an explicit
/// <c>--allow-unverified</c>/admin override when its detached signature verifies against one of
/// <see cref="TrustedKeys"/> and that key id is not listed in <see cref="RevokedKeyIds"/>.
/// </para>
/// <para>
/// <see cref="TrustedKeys"/> is deliberately <b>not</b> read from the feed. A feed is data; trusting
/// keys carried by the same data would let a compromised feed sign its own packages.
/// </para>
/// </remarks>
public sealed class ModuleStoreOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Modules:Store";

    /// <summary>
    /// The local feed directory (an <c>index.json</c> beside the packages). Relative paths resolve
    /// against the application base directory; when unset, <c>store-feed</c> is used.
    /// </summary>
    public string? LocalFeedPath { get; set; }

    /// <summary>
    /// Whether this server permits unverified installs <em>at all</em>. The default is
    /// <see langword="false"/>, and even when true each install must still opt in explicitly and is
    /// audited.
    /// </summary>
    public bool AllowUnverified { get; set; }

    /// <summary>
    /// Trusted signing keys, keyed by key id: <c>{ "store-2026": "&lt;base64 32-byte public key&gt;" }</c>.
    /// </summary>
    public Dictionary<string, string> TrustedKeys { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Key ids that must never be trusted again (rotation after a compromise). Removed from
    /// <see cref="TrustedKeys"/> at load time so a stale entry cannot keep verifying.
    /// </summary>
    public string[] RevokedKeyIds { get; set; } = [];

    /// <summary>
    /// How long to wait for a freshly installed module to report healthy before rolling back.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether the version replaced by an install is kept so
    /// <c>dotnetcloud module rollback</c> can restore it. Default: <see langword="true"/>.
    /// </summary>
    public bool KeepPreviousVersion { get; set; } = true;

    /// <summary>
    /// The trusted keys with <see cref="RevokedKeyIds"/> removed.
    /// </summary>
    /// <returns>The effective key id → base64 public key map.</returns>
    public IReadOnlyDictionary<string, string> GetEffectiveTrustedKeys()
    {
        var revoked = new HashSet<string>(RevokedKeyIds ?? [], StringComparer.Ordinal);
        var effective = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (keyId, publicKey) in TrustedKeys ?? [])
        {
            if (revoked.Contains(keyId) || string.IsNullOrWhiteSpace(publicKey))
            {
                continue;
            }

            effective[keyId] = publicKey.Trim();
        }

        return effective;
    }
}
