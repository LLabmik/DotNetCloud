using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using DotNetCloud.Core.Server.ModuleLoading;

namespace DotNetCloud.ModulePackaging;

/// <summary>Options controlling package verification.</summary>
public sealed class ModulePackageVerificationOptions
{
    /// <summary>
    /// Trusted signing keys, keyed by key id. A package signed by an unknown key id is rejected
    /// rather than accepted on the strength of its own file.
    /// </summary>
    public IReadOnlyDictionary<string, string> TrustedKeys { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Whether an unsigned package may be accepted (sideload only).</summary>
    public bool AllowUnsigned { get; init; }
}

/// <summary>The outcome of verifying a package.</summary>
/// <param name="IsValid">Whether every check passed.</param>
/// <param name="ModuleId">The module id from the file name and manifest, when readable.</param>
/// <param name="Version">The module version, when readable.</param>
/// <param name="KeyId">The signing key id, when a signature was present.</param>
/// <param name="Sha256">The package SHA-256, when it could be computed.</param>
/// <param name="EntryCount">The number of entries in the archive.</param>
/// <param name="UncompressedBytes">The total uncompressed size.</param>
/// <param name="Issues">Every fatal failure found; empty when <paramref name="IsValid"/> is true.</param>
/// <param name="Warnings">Non-fatal observations (for example a legacy v1 manifest).</param>
public sealed record ModulePackageVerification(
    bool IsValid,
    string? ModuleId,
    string? Version,
    string? KeyId,
    string? Sha256,
    int EntryCount,
    long UncompressedBytes,
    IReadOnlyList<PackageIssue> Issues,
    IReadOnlyList<PackageIssue> Warnings);

/// <summary>
/// Verifies a package before it is extracted: archive safety, manifest validity, identity match and
/// signature (F8/F13).
/// </summary>
/// <remarks>
/// Order matters. The archive structure is inspected first (a valid signature over a hostile archive
/// is still hostile), then the manifest is validated with the same loader the core uses, then the
/// signature and hash are checked. Nothing is extracted by this type.
/// </remarks>
public static class ModulePackageVerifier
{
    /// <summary>
    /// Verifies a package.
    /// </summary>
    /// <param name="packagePath">The package path.</param>
    /// <param name="signaturePath">The signature path, or <see langword="null"/> for the default.</param>
    /// <param name="options">Verification options; defaults are strict (signed only, no trusted keys).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="inlineSignature">
    /// A signature supplied by a feed index, used when no detached <c>.sig</c> file exists.
    /// </param>
    /// <returns>The verification outcome.</returns>
    public static async Task<ModulePackageVerification> VerifyAsync(
        string packagePath,
        string? signaturePath = null,
        ModulePackageVerificationOptions? options = null,
        CancellationToken cancellationToken = default,
        ModulePackageSignatureFile? inlineSignature = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        options ??= new ModulePackageVerificationOptions();

        var issues = new List<PackageIssue>();
        var warnings = new List<PackageIssue>();
        string? moduleId = null;
        string? version = null;
        string? keyId = null;
        string? sha256 = null;

        if (!File.Exists(packagePath))
        {
            return Failure(PackageIssueCodes.PackageNotFound, $"Package not found: {packagePath}");
        }

        var packageBytes = new FileInfo(packagePath).Length;
        if (packageBytes > ModulePackageLimits.MaxPackageBytes)
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.PackageTooLarge,
                $"Package is {packageBytes} bytes; the limit is {ModulePackageLimits.MaxPackageBytes}."));
        }

        if (!ModulePackageFormat.TryParseFileName(packagePath, out var fileNameId, out var fileNameVersion))
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.FileNameInvalid,
                $"'{Path.GetFileName(packagePath)}' is not named '<id>-<version>{ModulePackageFormat.Extension}'."));
        }
        else
        {
            moduleId = fileNameId;
            version = fileNameVersion;
        }

        int entryCount;
        long uncompressedBytes;

        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var entries = ArchiveSafetyInspector.Describe(archive);

            entryCount = entries.Count;
            uncompressedBytes = entries.Where(entry => !entry.IsDirectory).Sum(entry => entry.Length);

            issues.AddRange(ArchiveSafetyInspector.Inspect(entries));

            foreach (var required in ModulePackageFormat.RequiredEntryNames)
            {
                if (!entries.Any(entry => string.Equals(entry.FullName, required, StringComparison.Ordinal)))
                {
                    issues.Add(new PackageIssue(
                        PackageIssueCodes.MissingEntry,
                        $"Required entry '{required}' is missing.",
                        required));
                }
            }

            if (!entries.Any(entry => entry.FullName.StartsWith(
                    ModulePackageFormat.FilesDirectoryName + "/", StringComparison.Ordinal)))
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.MissingEntry,
                    $"The package contains no '{ModulePackageFormat.FilesDirectoryName}/<rid>/' binaries.",
                    ModulePackageFormat.FilesDirectoryName));
            }

            // The manifest is validated with the core's own loader, so packaging cannot drift from
            // runtime validation (strict v2 fields, minCoreVersion, data-store and UI rules).
            var manifestEntry = archive.GetEntry(ModulePackageFormat.ManifestEntryName);
            if (manifestEntry is not null)
            {
                if (manifestEntry.Length > ModulePackageLimits.MaxManifestBytes)
                {
                    issues.Add(new PackageIssue(
                        PackageIssueCodes.ManifestInvalid,
                        $"manifest.json is {manifestEntry.Length} bytes; the limit is {ModulePackageLimits.MaxManifestBytes}.",
                        ModulePackageFormat.ManifestEntryName));
                }
                else
                {
                    var manifestIssues = await ValidateManifestAsync(
                        manifestEntry, moduleId, version, warnings, cancellationToken);

                    issues.AddRange(manifestIssues);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.InvalidZip,
                $"The file is not a readable ZIP archive: {ex.Message}"));
            return Finish(issues, warnings, moduleId, version, keyId, sha256, entryCount: 0, uncompressedBytes: 0);
        }

        sha256 = await DeterministicPackageWriter.ComputeSha256Async(packagePath, cancellationToken);

        var effectiveSignaturePath = signaturePath ?? ModulePackageFormat.GetSignaturePath(packagePath);
        issues.AddRange(VerifySignature(
            packagePath, effectiveSignaturePath, sha256, options, inlineSignature, out keyId));

        return Finish(issues, warnings, moduleId, version, keyId, sha256, entryCount, uncompressedBytes);
    }

    /// <summary>
    /// Extracts a package into a directory.
    /// </summary>
    /// <param name="packagePath">The package path.</param>
    /// <param name="destinationDirectory">The extraction root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The relative paths extracted, sorted ordinal.</returns>
    /// <exception cref="ModulePackageException">The archive is unsafe or an entry escapes the root.</exception>
    public static async Task<IReadOnlyList<string>> ExtractAsync(
        string packagePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        var root = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(root);

        var extracted = new List<string>();

        using var archive = ZipFile.OpenRead(packagePath);
        ArchiveSafetyInspector.EnsureSafe(archive);

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            var target = Path.GetFullPath(Path.Combine(root, entry.FullName));

            // Defence in depth: the inspector already rejected traversal, but the resolved path is
            // what the file system will actually write, so it is checked again here.
            var relative = Path.GetRelativePath(root, target);
            var firstSegment = relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar])[0];
            if (string.Equals(relative, ".", StringComparison.Ordinal)
                || Path.IsPathRooted(relative)
                || string.Equals(firstSegment, "..", StringComparison.Ordinal))
            {
                throw new ModulePackageException(
                    $"Entry '{entry.FullName}' resolves outside the extraction root.",
                    [new PackageIssue(PackageIssueCodes.PathTraversal, "Entry escapes the extraction root.", entry.FullName)]);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            await using (var source = entry.Open())
            await using (var destination = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            extracted.Add(entry.FullName);
        }

        extracted.Sort(StringComparer.Ordinal);
        return extracted;
    }

    private static async Task<IReadOnlyList<PackageIssue>> ValidateManifestAsync(
        ZipArchiveEntry manifestEntry,
        string? expectedId,
        string? expectedVersion,
        List<PackageIssue> warnings,
        CancellationToken cancellationToken)
    {
        var issues = new List<PackageIssue>();
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(), "dnc-package-verify-" + Guid.CreateVersion7().ToString("N"));

        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            var manifestPath = Path.Combine(temporaryDirectory, ModulePackageFormat.ManifestEntryName);

            await using (var source = manifestEntry.Open())
            await using (var destination = File.Create(manifestPath))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            var loader = new ModuleManifestLoader(NullLogger<ModuleManifestLoader>.Instance);

            // Structure is validated against the manifest's own id; identity against the package
            // name afterwards, so a mismatch is reported as a mismatch rather than a parse error.
            var manifestId = PeekManifestId(manifestPath);
            var result = loader.LoadAndValidate(manifestPath, manifestId);

            if (!result.IsValid || result.Manifest is null)
            {
                foreach (var error in result.Errors)
                {
                    issues.Add(new PackageIssue(PackageIssueCodes.ManifestInvalid, error));
                }

                return issues;
            }

            var manifest = result.Manifest;

            if (expectedId is not null && !string.Equals(manifest.Id, expectedId, StringComparison.Ordinal))
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.ManifestMismatch,
                    $"manifest id '{manifest.Id}' does not match the package name '{expectedId}'."));
            }

            if (expectedVersion is not null
                && !string.Equals(manifest.Version, expectedVersion, StringComparison.Ordinal))
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.ManifestMismatch,
                    $"manifest version '{manifest.Version}' does not match the package name '{expectedVersion}'."));
            }

            if (ModuleCompatibility.TryGetIncompatibilityReason(manifest, out var reason))
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.IncompatibleCore,
                    reason ?? "The manifest is incompatible with this core."));
            }

            if (manifest.ManifestVersion < 2)
            {
                // Accepted for bundled first-party modules until Track C migrates them; recorded so
                // the packer output is honest about what it contains.
                warnings.Add(new PackageIssue(
                    "MANIFEST_LEGACY_V1",
                    "The manifest is v1 (legacy). Non-required modules must ship a v2 manifest at launch."));
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new PackageIssue(PackageIssueCodes.ManifestInvalid, $"Invalid manifest JSON: {ex.Message}"));
        }
        finally
        {
            try
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best effort: a leftover temporary directory is not a verification failure.
            }
        }

        return issues;
    }

    private static IReadOnlyList<PackageIssue> VerifySignature(
        string packagePath,
        string signaturePath,
        string sha256,
        ModulePackageVerificationOptions options,
        ModulePackageSignatureFile? inlineSignature,
        out string? keyId)
    {
        var issues = new List<PackageIssue>();
        keyId = null;

        ModulePackageSignatureFile signature;
        if (File.Exists(signaturePath))
        {
            try
            {
                signature = ModulePackageSigner.Read(signaturePath);
            }
            catch (ModulePackageException ex)
            {
                issues.Add(new PackageIssue(PackageIssueCodes.SignatureInvalid, ex.Message));
                return issues;
            }
        }
        else if (inlineSignature is not null)
        {
            signature = inlineSignature;
        }
        else
        {
            if (!options.AllowUnsigned)
            {
                issues.Add(new PackageIssue(
                    PackageIssueCodes.SignatureMissing,
                    "The package is unsigned. Sideloading requires --allow-unverified."));
            }

            return issues;
        }

        keyId = signature.KeyId;

        if (!string.Equals(signature.Algorithm, Ed25519Signature.AlgorithmName, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.SignatureInvalid,
                $"Unsupported signature algorithm '{signature.Algorithm}'; only '{Ed25519Signature.AlgorithmName}' is accepted."));
            return issues;
        }

        if (!string.Equals(signature.Package, Path.GetFileName(packagePath), StringComparison.Ordinal))
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.SignatureInvalid,
                $"The signature is for '{signature.Package}', not '{Path.GetFileName(packagePath)}'."));
        }

        if (!string.Equals(signature.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.HashMismatch,
                "The package hash does not match the hash in the signature file."));
            return issues;
        }

        if (!options.TrustedKeys.TryGetValue(signature.KeyId, out var publicKey))
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.SignatureUntrustedKey,
                $"Signing key '{signature.KeyId}' is not trusted by this verifier."));
            return issues;
        }

        var digest = Convert.FromHexString(sha256);
        if (!Ed25519Signature.Verify(digest, publicKey, signature.Signature))
        {
            issues.Add(new PackageIssue(
                PackageIssueCodes.SignatureInvalid,
                "The Ed25519 signature does not verify against the trusted key."));
        }

        return issues;
    }

    private static string PeekManifestId(string manifestPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return document.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                ? id.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static ModulePackageVerification Failure(string code, string message) =>
        new(false, null, null, null, null, 0, 0, [new PackageIssue(code, message)], []);

    private static ModulePackageVerification Finish(
        List<PackageIssue> issues,
        List<PackageIssue> warnings,
        string? moduleId,
        string? version,
        string? keyId,
        string? sha256,
        int entryCount,
        long uncompressedBytes) =>
        new(
            issues.Count == 0,
            moduleId,
            version,
            keyId,
            sha256,
            entryCount,
            uncompressedBytes,
            issues,
            warnings);
}
