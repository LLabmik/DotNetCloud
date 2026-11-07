using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// One package in a local feed index.
/// </summary>
/// <param name="Id">Module id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Version">Module version.</param>
/// <param name="File">Package file name relative to the feed directory.</param>
/// <param name="Size">Package size in bytes.</param>
/// <param name="Sha256">Lower-case hex SHA-256 of the package.</param>
/// <param name="PublisherId">Publisher id from the manifest, when declared.</param>
/// <param name="DataStore">Manifest data-store mode (<c>isolated</c> or <c>none</c>).</param>
/// <param name="HandlesUserErasure">Whether the manifest implements the erasure contract.</param>
/// <param name="Capabilities">Declared capabilities.</param>
/// <param name="UserConsentScopes">Declared per-user consent scopes.</param>
/// <param name="KeyId">Signing key id, when signed.</param>
/// <param name="Signature">Base64 Ed25519 signature, when signed.</param>
public sealed record ModuleFeedEntry(
    string Id,
    string Name,
    string Version,
    string File,
    long Size,
    string Sha256,
    string? PublisherId = null,
    string? DataStore = null,
    bool HandlesUserErasure = false,
    IReadOnlyList<string>? Capabilities = null,
    IReadOnlyList<string>? UserConsentScopes = null,
    string? KeyId = null,
    string? Signature = null);

/// <summary>
/// A local feed: an <c>index.json</c> next to the package files.
/// </summary>
/// <remarks>
/// The local feed exists so the installer (B3) and integration tests can work entirely offline. It
/// is a trimmed version of the store index (§4.9) and carries the same identity, hash and signature
/// facts, so moving to the remote index does not change the verification path.
/// </remarks>
public sealed record ModuleFeedIndex
{
    /// <summary>Index schema version.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>When the index was last generated.</summary>
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>The packaged versions, ordered by id then version.</summary>
    public IReadOnlyList<ModuleFeedEntry> Packages { get; init; } = [];
}

/// <summary>Reads, writes and verifies a local package feed.</summary>
public static class ModuleFeed
{
    /// <summary>The index file name inside a feed directory.</summary>
    public const string IndexFileName = "index.json";

    /// <summary>
    /// Reads a feed index, returning an empty index when none exists.
    /// </summary>
    /// <param name="feedDirectory">The feed directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The index.</returns>
    public static async Task<ModuleFeedIndex> ReadAsync(
        string feedDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedDirectory);

        var indexPath = Path.Combine(Path.GetFullPath(feedDirectory), IndexFileName);
        if (!File.Exists(indexPath))
        {
            return new ModuleFeedIndex();
        }

        try
        {
            return JsonSerializer.Deserialize<ModuleFeedIndex>(
                await File.ReadAllTextAsync(indexPath, cancellationToken), SerializerOptions)
                ?? new ModuleFeedIndex();
        }
        catch (JsonException ex)
        {
            throw new ModulePackageException($"Feed index '{indexPath}' is not valid JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes a feed index deterministically (packages sorted by id then version).
    /// </summary>
    /// <param name="feedDirectory">The feed directory.</param>
    /// <param name="index">The index to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task WriteAsync(
        string feedDirectory, ModuleFeedIndex index, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedDirectory);
        ArgumentNullException.ThrowIfNull(index);

        var root = Path.GetFullPath(feedDirectory);
        Directory.CreateDirectory(root);

        var ordered = index with
        {
            Packages = index.Packages
                .OrderBy(entry => entry.Id, StringComparer.Ordinal)
                .ThenBy(entry => entry.Version, StringComparer.Ordinal)
                .ToList(),
        };

        await File.WriteAllTextAsync(
            Path.Combine(root, IndexFileName),
            JsonSerializer.Serialize(ordered, SerializerOptions) + Environment.NewLine,
            cancellationToken);
    }

    /// <summary>
    /// Adds (or replaces) a package in the feed and rewrites the index.
    /// </summary>
    /// <param name="feedDirectory">The feed directory.</param>
    /// <param name="packagePath">The package to add; it is expected to live in the feed directory.</param>
    /// <param name="signature">The detached signature, when the package is signed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entry that was written.</returns>
    public static async Task<ModuleFeedEntry> AddAsync(
        string feedDirectory,
        string packagePath,
        ModulePackageSignatureFile? signature,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        var root = Path.GetFullPath(feedDirectory);
        var package = Path.GetFullPath(packagePath);

        if (!File.Exists(package))
        {
            throw new ModulePackageException($"Package not found: {package}");
        }

        // A feed never records a path outside itself; a package from elsewhere is copied in.
        if (!string.Equals(Path.GetDirectoryName(package), root, StringComparison.Ordinal))
        {
            Directory.CreateDirectory(root);
            var destination = Path.Combine(root, Path.GetFileName(package));
            File.Copy(package, destination, overwrite: true);

            var sourceSignature = ModulePackageFormat.GetSignaturePath(package);
            if (File.Exists(sourceSignature))
            {
                File.Copy(sourceSignature, ModulePackageFormat.GetSignaturePath(destination), overwrite: true);
            }

            package = destination;
        }

        if (!ModulePackageFormat.TryParseFileName(package, out var moduleId, out var version))
        {
            throw new ModulePackageException(
                $"'{Path.GetFileName(package)}' is not named '<id>-<version>{ModulePackageFormat.Extension}'.");
        }

        var metadata = ReadManifestMetadata(package);
        var sha256 = await DeterministicPackageWriter.ComputeSha256Async(package, cancellationToken);
        var size = new FileInfo(package).Length;

        var entry = new ModuleFeedEntry(
            moduleId,
            metadata.Name ?? moduleId,
            version,
            Path.GetRelativePath(root, package).Replace('\\', '/'),
            size,
            sha256,
            metadata.PublisherId,
            metadata.DataStore,
            metadata.HandlesUserErasure,
            metadata.Capabilities,
            metadata.UserConsentScopes,
            signature?.KeyId,
            signature?.Signature);

        var existing = await ReadAsync(root, cancellationToken);
        var packages = existing.Packages
            .Where(existingEntry => !(string.Equals(existingEntry.Id, moduleId, StringComparison.Ordinal)
                                      && string.Equals(existingEntry.Version, version, StringComparison.Ordinal)))
            .Append(entry)
            .ToList();

        await WriteAsync(
            root,
            new ModuleFeedIndex
            {
                SchemaVersion = existing.SchemaVersion,
                GeneratedAtUtc = DateTime.UtcNow,
                Packages = packages,
            },
            cancellationToken);

        return entry;
    }

    /// <summary>
    /// Verifies a package listed in a feed, preferring the sibling <c>.sig</c> file and falling back
    /// to the signature recorded in the index.
    /// </summary>
    /// <param name="feedDirectory">The feed directory.</param>
    /// <param name="moduleId">The module id.</param>
    /// <param name="version">The module version.</param>
    /// <param name="options">Verification options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification outcome.</returns>
    public static async Task<ModulePackageVerification> VerifyEntryAsync(
        string feedDirectory,
        string moduleId,
        string version,
        ModulePackageVerificationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var root = Path.GetFullPath(feedDirectory);
        var index = await ReadAsync(root, cancellationToken);

        var entry = index.Packages.FirstOrDefault(
            candidate => string.Equals(candidate.Id, moduleId, StringComparison.Ordinal)
                         && string.Equals(candidate.Version, version, StringComparison.Ordinal));

        if (entry is null)
        {
            throw new ModulePackageException($"'{moduleId}' {version} is not in the feed at '{root}'.");
        }

        // The index is data, not authority: the file must resolve inside the feed directory.
        var packagePath = Path.GetFullPath(Path.Combine(root, entry.File));
        var relative = Path.GetRelativePath(root, packagePath);
        var firstSegment = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])[0];
        if (Path.IsPathRooted(relative) || string.Equals(firstSegment, "..", StringComparison.Ordinal))
        {
            throw new ModulePackageException($"Feed entry '{entry.File}' resolves outside the feed directory.");
        }

        var signaturePath = ModulePackageFormat.GetSignaturePath(packagePath);
        if (File.Exists(signaturePath))
        {
            return await ModulePackageVerifier.VerifyAsync(packagePath, signaturePath, options, cancellationToken);
        }

        var inlineSignature = entry is { KeyId: { } keyId, Signature: { } signatureValue }
            ? new ModulePackageSignatureFile
            {
                KeyId = keyId,
                File = Path.GetFileName(packagePath),
                Sha256 = entry.Sha256,
                Signature = signatureValue,
            }
            : null;

        return await ModulePackageVerifier.VerifyAsync(
            packagePath, signaturePath, options, cancellationToken, inlineSignature);
    }

    private sealed record ManifestMetadata(
        string? Name,
        string? PublisherId,
        string? DataStore,
        bool HandlesUserErasure,
        IReadOnlyList<string> Capabilities,
        IReadOnlyList<string> UserConsentScopes);

    private static ManifestMetadata ReadManifestMetadata(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var manifestEntry = archive.GetEntry(ModulePackageFormat.ManifestEntryName)
            ?? throw new ModulePackageException($"{ModulePackageFormat.ManifestEntryName} is missing from the package.");

        using var reader = new StreamReader(manifestEntry.Open());
        using var document = JsonDocument.Parse(reader.ReadToEnd(), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var root = document.RootElement;

        string? publisherId = null;
        if (root.TryGetProperty("publisher", out var publisher) && publisher.ValueKind == JsonValueKind.Object)
        {
            publisherId = ReadString(publisher, "id");
        }

        var capabilities = ReadStringArray(root, "capabilities", "requiredCapabilities");
        var scopes = ReadStringArray(root, "userConsentScopes");

        return new ManifestMetadata(
            ReadString(root, "name"),
            publisherId,
            ReadString(root, "dataStore"),
            root.TryGetProperty("handlesUserErasure", out var handles) && handles.ValueKind == JsonValueKind.True,
            capabilities,
            scopes);
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, params string[] properties)
    {
        foreach (var property in properties)
        {
            if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return array.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(value => value.Length > 0)
                .ToList();
        }

        return [];
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
