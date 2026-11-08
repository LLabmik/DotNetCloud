namespace DotNetCloud.ModulePackaging;

/// <summary>
/// Configuration for the module store client and the package installer, bound from the
/// <c>Modules:Store</c> section.
/// </summary>
/// <remarks>
/// <para>
/// Two sources implement the store client: a <b>local feed directory</b> or an explicit
/// <c>.dncpkg</c> file, and a <b>remote signed index</b> fetched over HTTPS
/// (<see cref="IndexUrl"/>). <see cref="TrustedKeys"/> is the whole trust configuration in both
/// cases: a package is only installed without an explicit
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
    /// <remarks>
    /// Ignored when <see cref="IndexUrl"/> is set: the remote index replaces the local feed rather
    /// than supplementing it, so one trust decision (which index) governs every install.
    /// </remarks>
    public string? LocalFeedPath { get; set; }

    /// <summary>
    /// The published store index (for example <c>https://modules.dotnetcloud.net/index/v1/index.json</c>).
    /// When set, the server fetches, verifies and caches that index instead of reading
    /// <see cref="LocalFeedPath"/>.
    /// </summary>
    /// <remarks>
    /// Must be <c>https</c> (plain <c>http</c> is accepted only for loopback, for development).
    /// The detached signature at <c>&lt;IndexUrl&gt;.sig</c> must verify against one of
    /// <see cref="TrustedKeys"/>; an index signed by any other key is rejected outright.
    /// </remarks>
    public string? IndexUrl { get; set; }

    /// <summary>
    /// Additional index URLs tried in order when <see cref="IndexUrl"/> cannot be fetched or fails
    /// verification. Every mirror must serve the same signed index.
    /// </summary>
    public string[] Mirrors { get; set; } = [];

    /// <summary>
    /// Where the fetched index and downloaded packages are cached. Relative paths resolve against
    /// the application base directory; when unset, <c>store-cache</c> is used.
    /// </summary>
    public string? CachePath { get; set; }

    /// <summary>
    /// How long a cached index is served before it is refetched. Default: one hour. When a refetch
    /// fails, the cached copy keeps being used and the failure is logged.
    /// </summary>
    public TimeSpan IndexCacheTtl { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Per-request timeout for the index and package downloads. Default: 60 seconds.
    /// </summary>
    public TimeSpan DownloadTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Extra origins (<c>scheme://host:port</c>) an index may serve packages from, in addition to
    /// the index's own origin. Empty by default: a download URL that points anywhere else is
    /// refused, so a compromised index cannot redirect installations to a third-party host.
    /// </summary>
    public string[] DownloadOrigins { get; set; } = [];

    /// <summary>
    /// The base URL that relative index <c>downloadUrl</c> values resolve against. Default: the
    /// index URL's origin (<c>https://modules.dotnetcloud.net/</c>), because the published
    /// catalogue uses site-root-relative paths such as <c>packages/&lt;id&gt;/&lt;version&gt;/…</c>
    /// while the index itself is served from <c>index/v1/index.json</c>.
    /// </summary>
    public string? DownloadBaseUrl { get; set; }

    /// <summary>
    /// Whether an index whose <c>generatedAt</c> is <em>older</em> than the cached one may replace
    /// it. Default <see langword="false"/>: an old index replayed by an attacker or a stale mirror
    /// would otherwise silently roll the catalogue back. Set it only to deliberately undo a
    /// publish.
    /// </summary>
    public bool AllowIndexRollback { get; set; }

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
