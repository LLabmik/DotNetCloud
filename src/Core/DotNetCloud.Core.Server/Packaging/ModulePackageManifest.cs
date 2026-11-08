using System.IO.Compression;
using System.Text.Json;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Server.ModuleLoading;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.ModulePackaging;

/// <summary>
/// The manifest facts of a package, projected from the core's own manifest model.
/// </summary>
/// <param name="Id">Module id.</param>
/// <param name="Name">Display name.</param>
/// <param name="Version">Module version.</param>
/// <param name="Description">Description.</param>
/// <param name="ManifestVersion">Manifest schema version.</param>
/// <param name="PublisherId">Publisher id, when declared.</param>
/// <param name="PublisherName">Publisher display name, when declared.</param>
/// <param name="PublisherUrl">Publisher home page, when declared.</param>
/// <param name="DataStoreMode">Requested data-store mode.</param>
/// <param name="HandlesUserErasure">Whether the module implements the user-erasure contract.</param>
/// <param name="Capabilities">Capabilities the module asks the core to grant.</param>
/// <param name="UserConsentScopes">Consent scopes the module may request per user.</param>
/// <param name="PublishedEvents">Event types the module publishes.</param>
/// <param name="SubscribedEvents">Event types the module subscribes to.</param>
/// <param name="ModuleApiVersion">Module API version the package targets.</param>
/// <param name="MinCoreVersion">Minimum core version.</param>
/// <param name="MaxCoreVersion">Maximum core version.</param>
/// <param name="MemoryLimitMb">Requested memory ceiling.</param>
/// <param name="ConnectionLimit">Requested database connection limit.</param>
public sealed record ModulePackageManifestInfo(
    string Id,
    string Name,
    string Version,
    string? Description,
    int ManifestVersion,
    string? PublisherId,
    string? PublisherName,
    string? PublisherUrl,
    ModuleDataStoreMode DataStoreMode,
    bool HandlesUserErasure,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> UserConsentScopes,
    IReadOnlyList<string> PublishedEvents,
    IReadOnlyList<string> SubscribedEvents,
    int? ModuleApiVersion,
    string? MinCoreVersion,
    string? MaxCoreVersion,
    int? MemoryLimitMb,
    int? ConnectionLimit);

/// <summary>
/// Reads the embedded <c>manifest.json</c> of a package using the same loader the core runs.
/// </summary>
/// <remarks>
/// The installer must never re-implement manifest validation: a package that the supervisor would
/// refuse at start-up has to be refused at install time, with the same rules. Both the verifier and
/// the installer therefore go through <see cref="ModuleManifestLoader"/>.
/// </remarks>
public static class ModulePackageManifest
{
    /// <summary>
    /// Reads and validates the manifest of a package without extracting it.
    /// </summary>
    /// <param name="packagePath">The package path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The manifest facts, or <see langword="null"/> when the manifest is missing or invalid.</returns>
    public static async Task<ModulePackageManifestInfo?> TryReadAsync(
        string packagePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        if (!File.Exists(packagePath))
        {
            return null;
        }

        try
        {
            using var archive = ZipFile.OpenRead(packagePath);
            var entry = archive.GetEntry(ModulePackageFormat.ManifestEntryName);
            if (entry is null || entry.Length > ModulePackageLimits.MaxManifestBytes)
            {
                return null;
            }

            var load = await LoadAsync(entry, cancellationToken);
            return load.Manifest is null ? null : ToInfo(load.Manifest);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads and validates a manifest that already lives on disk (an installed module directory).
    /// </summary>
    /// <param name="manifestPath">Path to the <c>manifest.json</c> file.</param>
    /// <returns>The manifest facts, or <see langword="null"/> when the manifest is missing or invalid.</returns>
    public static ModulePackageManifestInfo? TryReadFile(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var loader = new ModuleManifestLoader(NullLogger<ModuleManifestLoader>.Instance);
        var result = loader.LoadAndValidate(manifestPath, PeekManifestId(manifestPath));

        return result.IsValid && result.Manifest is not null ? ToInfo(result.Manifest) : null;
    }

    /// <summary>
    /// Extracts the manifest entry to a temporary file and runs the core loader over it.
    /// </summary>
    /// <param name="manifestEntry">The <c>manifest.json</c> archive entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validated manifest (or null) and any structural findings.</returns>
    internal static async Task<PackageManifestLoadResult> LoadAsync(
        ZipArchiveEntry manifestEntry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifestEntry);

        var issues = new List<PackageIssue>();
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(), "dnc-package-manifest-" + Guid.CreateVersion7().ToString("N"));

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
            // name separately, so a mismatch is reported as a mismatch rather than a parse error.
            var manifestId = PeekManifestId(manifestPath);
            var result = loader.LoadAndValidate(manifestPath, manifestId);

            if (!result.IsValid || result.Manifest is null)
            {
                foreach (var error in result.Errors)
                {
                    issues.Add(new PackageIssue(PackageIssueCodes.ManifestInvalid, error));
                }

                return new PackageManifestLoadResult(null, issues);
            }

            return new PackageManifestLoadResult(result.Manifest, issues);
        }
        catch (JsonException ex)
        {
            issues.Add(new PackageIssue(PackageIssueCodes.ManifestInvalid, $"Invalid manifest JSON: {ex.Message}"));
            return new PackageManifestLoadResult(null, issues);
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
    }

    /// <summary>Projects the core manifest model onto the public package facts.</summary>
    internal static ModulePackageManifestInfo ToInfo(ModuleManifestData manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return new ModulePackageManifestInfo(
            manifest.Id,
            manifest.Name,
            manifest.Version,
            manifest.Description,
            manifest.ManifestVersion,
            manifest.Publisher?.Id,
            manifest.Publisher?.Name,
            manifest.Publisher?.Url,
            manifest.EffectiveDataStoreMode,
            manifest.HandlesUserErasure ?? false,
            manifest.EffectiveCapabilities,
            manifest.UserConsentScopes,
            manifest.EffectivePublishedEvents,
            manifest.EffectiveSubscribedEvents,
            manifest.ModuleApiVersion,
            manifest.MinCoreVersion,
            manifest.MaxCoreVersion,
            manifest.EffectiveMemoryLimitMb,
            manifest.Resources?.ConnectionLimit);
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
}

/// <summary>The outcome of loading a package manifest.</summary>
/// <param name="Manifest">The validated manifest, or null when it is invalid.</param>
/// <param name="Issues">Structural findings.</param>
internal sealed record PackageManifestLoadResult(
    ModuleManifestData? Manifest,
    IReadOnlyList<PackageIssue> Issues);
