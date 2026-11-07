using System.IO.Compression;
using System.Text;
using DotNetCloud.ModulePackaging;

namespace DotNetCloud.ModulePackaging.Tests;

/// <summary>
/// Builds throwaway package staging directories and archives for the packaging tests.
/// </summary>
internal sealed class PackageFixture : IDisposable
{
    /// <summary>The canonical v1 manifest used by the fixture.</summary>
    public const string DefaultModuleId = "dotnetcloud.example";

    private const string V1ManifestTemplate = """
        {
          "id": "__ID__",
          "name": "Example",
          "version": "__VERSION__",
          "description": "Fixture module",
          "author": "DotNetCloud",
          "requiredCapabilities": ["IAuditLogger"],
          "publishedEvents": [],
          "subscribedEvents": [],
          "minCoreVersion": "0.7.0"
        }
        """;

    /// <summary>Initializes a new fixture with its own temporary root.</summary>
    public PackageFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "dnc-pack-tests-" + Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(Root);
        KeyPair = Ed25519Signature.GenerateKeyPair("test-key");
    }

    /// <summary>Gets the fixture's temporary root directory.</summary>
    public string Root { get; }

    /// <summary>Gets the signing key pair used by signed packages.</summary>
    public ModuleSigningKeyPair KeyPair { get; }

    /// <summary>Gets the trusted-key map for <see cref="KeyPair"/>.</summary>
    public IReadOnlyDictionary<string, string> TrustedKeys =>
        new Dictionary<string, string>(StringComparer.Ordinal) { [KeyPair.KeyId] = KeyPair.PublicKey };

    /// <summary>Builds a staging directory with the standard layout.</summary>
    /// <param name="moduleId">Manifest module id and directory name.</param>
    /// <param name="version">Manifest version.</param>
    /// <param name="manifestOverride">Replaces the generated manifest when supplied.</param>
    /// <param name="skipEntries">Required top-level entries to omit.</param>
    /// <param name="rid">Runtime identifier directory under <c>files/</c>.</param>
    /// <returns>The staging directory path.</returns>
    public string CreateStaging(
        string moduleId = DefaultModuleId,
        string version = "1.0.0",
        string? manifestOverride = null,
        string[]? skipEntries = null,
        string? rid = "linux-x64")
    {
        var staging = Path.Combine(Root, "staging-" + Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(staging);

        File.WriteAllText(
            Path.Combine(staging, ModulePackageFormat.ManifestEntryName),
            manifestOverride ?? V1ManifestTemplate
                .Replace("__ID__", moduleId, StringComparison.Ordinal)
                .Replace("__VERSION__", version, StringComparison.Ordinal));

        var required = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ModulePackageFormat.LicenseEntryName] = "licence text",
            [ModulePackageFormat.ReadmeEntryName] = "# Example",
            [ModulePackageFormat.ChangelogEntryName] = "# Changelog",
        };

        foreach (var (name, content) in required)
        {
            if (skipEntries is not null && skipEntries.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            File.WriteAllText(Path.Combine(staging, name), content);
        }

        if (rid is not null)
        {
            var binaries = Path.Combine(staging, ModulePackageFormat.FilesDirectoryName, rid);
            Directory.CreateDirectory(binaries);
            File.WriteAllText(Path.Combine(binaries, moduleId + ".dll"), "fake assembly");
        }

        return staging;
    }

    /// <summary>Packs a staging directory, optionally signing it.</summary>
    /// <param name="staging">The staging directory.</param>
    /// <param name="sign">Whether to produce a detached signature.</param>
    /// <returns>The package path and build result.</returns>
    public async Task<(string PackagePath, PackageBuildResult Build)> PackAsync(
        string staging, bool sign = true)
    {
        var (moduleId, version) = ReadStagedIdentity(staging);
        var outputDirectory = Path.Combine(Root, "out-" + Guid.CreateVersion7().ToString("N"));
        var packagePath = Path.Combine(outputDirectory, ModulePackageFormat.BuildFileName(moduleId, version));
        var entries = DeterministicPackageWriter.CollectFromDirectory(staging);
        var build = await DeterministicPackageWriter.WriteAsync(packagePath, entries);

        if (sign)
        {
            await ModulePackageSigner.SignAsync(build.PackagePath, KeyPair);
        }

        return (build.PackagePath, build);
    }

    private static (string ModuleId, string Version) ReadStagedIdentity(string staging)
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(staging, ModulePackageFormat.ManifestEntryName)));

        var id = document.RootElement.GetProperty("id").GetString()!;
        var version = document.RootElement.GetProperty("version").GetString()!;
        return (id, version);
    }

    /// <summary>Writes a ZIP whose entries are supplied verbatim (used to build hostile archives).</summary>
    /// <param name="fileName">The package file name, including <c>.dncpkg</c>.</param>
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

    /// <summary>Removes the fixture's temporary root.</summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort.
        }
    }
}
