using System.Text.Json;
using DotNetCloud.Core.Modules.Store;
using DotNetCloud.Core.Server.ModuleLoading;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// The published store index (<c>index/v1/index.json</c>) as served by `modules.dotnetcloud.net`.
/// </summary>
/// <remarks>
/// This is a different shape from the local feed: one package document lists every version, each
/// with its own hash, signature and withdrawal flag. Parsing is strict about the fields the installer
/// relies on and tolerant about new ones, so the store can add review metadata without breaking
/// older clients.
/// </remarks>
public sealed class ModuleStoreIndex
{
    /// <summary>The only index schema version this client understands.</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>Gets the index schema version.</summary>
    public int SchemaVersion { get; init; } = SupportedSchemaVersion;

    /// <summary>Gets when the index was generated (the anti-replay clock).</summary>
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Gets the published packages.</summary>
    public IReadOnlyList<ModuleStoreIndexPackage> Packages { get; init; } = [];
}

/// <summary>One package in the store index, with every published version.</summary>
public sealed class ModuleStoreIndexPackage
{
    /// <summary>Gets the module id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets the display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the publishing organisation, when declared.</summary>
    public ModuleStoreIndexPublisher? Publisher { get; init; }

    /// <summary>Gets the short summary shown in the catalogue.</summary>
    public string? Summary { get; init; }

    /// <summary>Gets the source repository URL, when declared.</summary>
    public string? Source { get; init; }

    /// <summary>Gets the published versions, in the index's order.</summary>
    public IReadOnlyList<ModuleStoreIndexVersion> Versions { get; init; } = [];
}

/// <summary>The publishing organisation of a store package.</summary>
public sealed class ModuleStoreIndexPublisher
{
    /// <summary>Gets the publisher id.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the publisher display name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets whether the store has verified the publisher.</summary>
    public bool Verified { get; init; }

    /// <summary>Gets whether the publisher is the DotNetCloud project itself.</summary>
    public bool Official { get; init; }
}

/// <summary>One published version of a store package.</summary>
public sealed class ModuleStoreIndexVersion
{
    /// <summary>Gets the module version.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets the trust tier the store derived for this version (<c>official</c>,
    /// <c>verified-publisher</c> or <c>community</c>), or null/unknown when the index predates the
    /// field or a newer store introduced a tier this client does not know.
    /// </summary>
    public string? TrustTier { get; init; }

    /// <summary>Gets the minimum core version the package requires.</summary>
    public string? MinCoreVersion { get; init; }

    /// <summary>Gets the module API version the package was built against.</summary>
    public int ModuleApiVersion { get; init; }

    /// <summary>Gets the manifest data-store mode (<c>isolated</c> or <c>none</c>).</summary>
    public string? DataStore { get; init; }

    /// <summary>Gets the capabilities the package requests.</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>Gets the per-user consent scopes the package requests.</summary>
    public IReadOnlyList<string> UserConsentScopes { get; init; } = [];

    /// <summary>Gets when the version was published.</summary>
    public DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// Gets the package URL: either relative to the index directory (the usual case) or an absolute
    /// <c>https</c> URL on an allowed origin.
    /// </summary>
    public string DownloadUrl { get; init; } = string.Empty;

    /// <summary>Gets the package size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>Gets the lower-case hex SHA-256 of the package bytes.</summary>
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>Gets the detached signature published with the version, when signed.</summary>
    public ModuleStoreIndexSignature? Signature { get; init; }

    /// <summary>Gets the store's review status for the version.</summary>
    public string? ReviewStatus { get; init; }

    /// <summary>Gets whether the version has been withdrawn.</summary>
    public bool Yanked { get; init; }

    /// <summary>Gets the operator-facing withdrawal reason, when withdrawn.</summary>
    public string? YankReason { get; init; }

    /// <summary>
    /// Gets the withdrawal severity (<c>deprecation</c> or <c>security</c>), when withdrawn. A
    /// <c>security</c> withdrawal additionally warns servers that already run the version.
    /// </summary>
    public string? YankedSeverity { get; init; }
}

/// <summary>The Ed25519 signature a store index publishes for a package version.</summary>
public sealed class ModuleStoreIndexSignature
{
    /// <summary>Gets the signing key id.</summary>
    public string? KeyId { get; init; }

    /// <summary>Gets the base64 Ed25519 signature over the package digest.</summary>
    public string? Value { get; init; }
}

/// <summary>
/// Parses and verifies a store index.
/// </summary>
/// <remarks>
/// Two independent checks gate everything else: the detached index signature (proving the store
/// produced this document) and per-entry validation of the fields the installer consumes. Nothing
/// here trusts a URL, a hash or a size until both have passed.
/// </remarks>
public static class ModuleStoreIndexReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Reads a store index from raw bytes.</summary>
    /// <param name="indexBytes">The exact bytes that were signed.</param>
    /// <returns>The parsed index.</returns>
    /// <exception cref="ModulePackageException">The document is not a valid, supported store index.</exception>
    public static ModuleStoreIndex Parse(ReadOnlySpan<byte> indexBytes)
    {
        ModuleStoreIndex? index;

        try
        {
            index = JsonSerializer.Deserialize<ModuleStoreIndex>(indexBytes, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new ModulePackageException($"The store index is not valid JSON: {ex.Message}");
        }

        if (index is null)
        {
            throw new ModulePackageException("The store index is empty.");
        }

        Validate(index);
        return index;
    }

    /// <summary>
    /// Verifies the detached index signature against the pinned keys.
    /// </summary>
    /// <param name="indexBytes">The exact bytes that were served as the index.</param>
    /// <param name="signature">The decoded detached signature document.</param>
    /// <param name="trustedKeys">Key id → base64 public key.</param>
    /// <returns>The key id that signed the index.</returns>
    /// <exception cref="ModulePackageException">The signature is missing, malformed or untrusted.</exception>
    public static string VerifySignature(
        ReadOnlySpan<byte> indexBytes,
        ModulePackageSignatureFile signature,
        IReadOnlyDictionary<string, string> trustedKeys)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(trustedKeys);

        if (!string.Equals(signature.Algorithm, Ed25519Signature.AlgorithmName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ModulePackageException(
                $"The store index signature uses unsupported algorithm '{signature.Algorithm}'.");
        }

        var digest = System.Security.Cryptography.SHA256.HashData(indexBytes);
        var digestHex = Convert.ToHexStringLower(digest);

        if (!string.Equals(signature.Sha256, digestHex, StringComparison.OrdinalIgnoreCase))
        {
            throw new ModulePackageException(
                "The store index does not match its signature: the SHA-256 of the served bytes differs from the signed hash.");
        }

        if (!trustedKeys.TryGetValue(signature.KeyId, out var publicKey)
            || string.IsNullOrWhiteSpace(publicKey))
        {
            throw new ModulePackageException(
                $"The store index is signed by '{signature.KeyId}', which this server does not trust.");
        }

        if (!Ed25519Signature.Verify(digest, publicKey.Trim(), signature.Signature))
        {
            throw new ModulePackageException("The store index signature does not verify.");
        }

        return signature.KeyId;
    }

    private static void Validate(ModuleStoreIndex index)
    {
        if (index.SchemaVersion != ModuleStoreIndex.SupportedSchemaVersion)
        {
            throw new ModulePackageException(
                $"The store index schema version {index.SchemaVersion} is not supported "
                + $"(this server understands {ModuleStoreIndex.SupportedSchemaVersion}).");
        }

        if (index.GeneratedAt == default)
        {
            throw new ModulePackageException("The store index has no generatedAt timestamp.");
        }

        if (index.Packages is null)
        {
            throw new ModulePackageException("The store index has no packages array.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var package in index.Packages)
        {
            ArgumentNullException.ThrowIfNull(package);

            if (!ModuleStorePaths.IsSafeSegment(package.Id))
            {
                throw new ModulePackageException($"The store index lists an unusable package id '{package.Id}'.");
            }

            if (package.Versions is null)
            {
                throw new ModulePackageException($"The store index entry '{package.Id}' has no versions array.");
            }

            foreach (var version in package.Versions)
            {
                ArgumentNullException.ThrowIfNull(version);

                var identity = package.Id + "@" + version.Version;

                if (!ModuleStorePaths.IsSafeSegment(version.Version))
                {
                    throw new ModulePackageException($"The store index lists an unusable version for '{package.Id}'.");
                }

                if (!ModuleCompatibility.TryParseVersion(version.Version, out _))
                {
                    throw new ModulePackageException($"The store index lists a non-semver version '{identity}'.");
                }

                if (string.IsNullOrWhiteSpace(version.DownloadUrl))
                {
                    throw new ModulePackageException($"The store index entry '{identity}' has no downloadUrl.");
                }

                if (version.Sha256 is null || !IsHexSha256(version.Sha256))
                {
                    throw new ModulePackageException($"The store index entry '{identity}' has an invalid SHA-256.");
                }

                if (version.Size < 0 || version.Size > ModulePackageLimits.MaxPackageBytes)
                {
                    throw new ModulePackageException($"The store index entry '{identity}' has an unusable size.");
                }

                if (!seen.Add(identity))
                {
                    throw new ModulePackageException($"The store index lists '{identity}' twice.");
                }

                // The tier is derived data, so it must not contradict the publisher flags it is
                // derived from. Unknown values are tolerated (a newer store may add a tier) and are
                // surfaced as "unspecified" rather than guessed at.
                var tier = ModuleStoreTrustTiers.Parse(version.TrustTier);

                if (tier == ModuleStoreTrustTier.Official
                    && (package.Publisher is null || !package.Publisher.Official || !package.Publisher.Verified))
                {
                    throw new ModulePackageException(
                        $"The store index marks '{identity}' official, but its publisher is not the verified DotNetCloud publisher.");
                }

                if (tier == ModuleStoreTrustTier.VerifiedPublisher && package.Publisher?.Verified != true)
                {
                    throw new ModulePackageException(
                        $"The store index marks '{identity}' verified-publisher, but the publisher is not verified.");
                }

                // Severity decides whether a server that already runs this version is warned, so a
                // value outside the vocabulary is refused rather than silently treated as benign.
                if (version.YankedSeverity is { Length: > 0 } severity
                    && severity is not ("deprecation" or "security"))
                {
                    throw new ModulePackageException(
                        $"The store index entry '{identity}' has an unknown withdrawal severity '{severity}'.");
                }
            }
        }
    }

    private static bool IsHexSha256(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
