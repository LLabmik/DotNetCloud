using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using DotNetCloud.ModulePackaging;

namespace DotNetCloud.Core.Server.Tests.Packaging;

/// <summary>
/// Builds throwaway signed packages and the directories the installer works in.
/// </summary>
internal sealed class PackageInstallerFixture : IDisposable
{
    private const string V2ManifestTemplate = """
        {
          "manifestVersion": 2,
          "id": "__ID__",
          "name": "Receipts",
          "version": "__VERSION__",
          "description": "Fixture module",
          "publisher": { "id": "example-org", "name": "Example Org", "url": "https://example.org" },
          "moduleApiVersion": 1,
          "minCoreVersion": "0.1.0",
          "dataStore": "__DATASTORE__",
          "handlesUserErasure": true,
          "capabilities": ["IAuditLogger", "IStorageProvider"],
          "userConsentScopes": ["files:read"],
          "events": { "publishes": ["ThingCreatedEvent"], "subscribes": [] }
        }
        """;

    /// <summary>The module id used by most tests.</summary>
    public const string ModuleId = "org.example.receipts";

    /// <summary>Initializes a new fixture with its own temporary root.</summary>
    public PackageInstallerFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "dnc-installer-tests-" + Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ModulesRoot);
        Directory.CreateDirectory(FeedDirectory);
        KeyPair = Ed25519Signature.GenerateKeyPair("test-store-key");
    }

    /// <summary>Gets the fixture's temporary root.</summary>
    public string Root { get; }

    /// <summary>Gets the modules directory the installer targets.</summary>
    public string ModulesRoot => Path.Combine(Root, "modules");

    /// <summary>Gets the local feed directory.</summary>
    public string FeedDirectory => Path.Combine(Root, "feed");

    /// <summary>Gets the signing key pair used by signed packages.</summary>
    public ModuleSigningKeyPair KeyPair { get; }

    /// <summary>Gets the trusted-key map for <see cref="KeyPair"/>.</summary>
    public IReadOnlyDictionary<string, string> TrustedKeys =>
        new Dictionary<string, string>(StringComparer.Ordinal) { [KeyPair.KeyId] = KeyPair.PublicKey };

    /// <summary>Gets the runtime identifier the tests package for (so the installer selects it).</summary>
    public static string HostRuntime => RuntimeInformation.RuntimeIdentifier;

    /// <summary>Builds the v2 manifest used by the tests.</summary>
    /// <param name="moduleId">Module id.</param>
    /// <param name="version">Module version.</param>
    /// <param name="dataStore"><c>none</c> or <c>isolated</c>.</param>
    /// <returns>The manifest JSON.</returns>
    public static string BuildManifest(string moduleId, string version, string dataStore = "none") =>
        V2ManifestTemplate
            .Replace("__ID__", moduleId, StringComparison.Ordinal)
            .Replace("__VERSION__", version, StringComparison.Ordinal)
            .Replace("__DATASTORE__", dataStore, StringComparison.Ordinal);

    /// <summary>Creates a staging directory laid out like <c>dotnetcloud-pack</c> output.</summary>
    /// <param name="moduleId">Module id.</param>
    /// <param name="version">Module version.</param>
    /// <param name="manifestOverride">Replaces the generated manifest.</param>
    /// <param name="rid">The <c>files/&lt;rid&gt;</c> directory to create (defaults to the host RID).</param>
    /// <param name="binaryContent">Content written to the module assembly.</param>
    /// <returns>The staging directory.</returns>
    public string CreateStaging(
        string moduleId = ModuleId,
        string version = "1.0.0",
        string? manifestOverride = null,
        string? rid = null,
        string binaryContent = "fake assembly")
    {
        var staging = Path.Combine(Root, "staging-" + Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(staging);

        File.WriteAllText(
            Path.Combine(staging, ModulePackageFormat.ManifestEntryName),
            manifestOverride ?? BuildManifest(moduleId, version));

        File.WriteAllText(Path.Combine(staging, ModulePackageFormat.LicenseEntryName), "licence text");
        File.WriteAllText(Path.Combine(staging, ModulePackageFormat.ReadmeEntryName), "# Receipts");
        File.WriteAllText(Path.Combine(staging, ModulePackageFormat.ChangelogEntryName), "# Changelog");

        var binaries = Path.Combine(staging, ModulePackageFormat.FilesDirectoryName, rid ?? HostRuntime);
        Directory.CreateDirectory(binaries);
        File.WriteAllText(Path.Combine(binaries, moduleId + ".dll"), binaryContent);

        return staging;
    }

    /// <summary>Packs (and optionally signs) a staging directory.</summary>
    /// <param name="staging">The staging directory.</param>
    /// <param name="sign">Whether to write a detached signature.</param>
    /// <param name="signWith">The key to sign with (defaults to <see cref="KeyPair"/>).</param>
    /// <returns>The package path.</returns>
    public async Task<string> PackAsync(
        string staging, bool sign = true, ModuleSigningKeyPair? signWith = null)
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(staging, ModulePackageFormat.ManifestEntryName)));

        var id = document.RootElement.GetProperty("id").GetString()!;
        var version = document.RootElement.GetProperty("version").GetString()!;

        var outputDirectory = Path.Combine(Root, "out-" + Guid.CreateVersion7().ToString("N"));
        var packagePath = Path.Combine(outputDirectory, ModulePackageFormat.BuildFileName(id, version));

        var entries = DeterministicPackageWriter.CollectFromDirectory(staging);
        var build = await DeterministicPackageWriter.WriteAsync(packagePath, entries);

        if (sign)
        {
            await ModulePackageSigner.SignAsync(build.PackagePath, signWith ?? KeyPair);
        }

        return build.PackagePath;
    }

    /// <summary>Convenience: stage, pack and sign in one call.</summary>
    /// <param name="version">Module version.</param>
    /// <param name="sign">Whether to sign.</param>
    /// <param name="manifestOverride">Overrides the manifest.</param>
    /// <param name="moduleId">Module id.</param>
    /// <param name="signWith">The key to sign with (defaults to <see cref="KeyPair"/>).</param>
    /// <returns>The package path.</returns>
    public async Task<string> CreatePackageAsync(
        string version = "1.0.0",
        bool sign = true,
        string? manifestOverride = null,
        string moduleId = ModuleId,
        ModuleSigningKeyPair? signWith = null) =>
        await PackAsync(CreateStaging(moduleId, version, manifestOverride), sign, signWith);

    /// <summary>Writes a raw archive whose entries are supplied verbatim (hostile shapes).</summary>
    /// <param name="fileName">The file name, including <c>.dncpkg</c>.</param>
    /// <param name="entries">Entry name → content.</param>
    /// <returns>The archive path.</returns>
    public string WriteRawArchive(string fileName, IEnumerable<(string Name, string Content)> entries)
    {
        var path = Path.Combine(Root, fileName);

        using (var stream = File.Create(path))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        return path;
    }

    /// <summary>Writes a local feed index listing the given packages.</summary>
    /// <param name="entries">The packages to list.</param>
    /// <returns>A task that completes when the index is written.</returns>
    public async Task WriteFeedIndexAsync(params ModuleFeedEntry[] entries)
    {
        await ModuleFeed.WriteAsync(
            FeedDirectory,
            new ModuleFeedIndex { GeneratedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Packages = entries });
    }

    /// <summary>Writes the feed's <c>yanked.json</c>.</summary>
    /// <param name="moduleId">Module id.</param>
    /// <param name="version">Withdrawn version.</param>
    /// <param name="reason">Withdrawal reason.</param>
    /// <returns>A task that completes when the file is written.</returns>
    public async Task WriteYankedAsync(string moduleId, string version, string reason = "Removed a credential leak")
    {
        var record = new ModuleFeedYankRecord
        {
            Yanked =
            [
                new ModuleFeedYankedVersion
                {
                    Id = moduleId,
                    Version = version,
                    Severity = "security",
                    Reason = reason,
                    YankedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                },
            ],
        };

        await File.WriteAllTextAsync(
            Path.Combine(FeedDirectory, ModuleFeed.YankFileName),
            System.Text.Json.JsonSerializer.Serialize(record, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort.
        }
    }
}
