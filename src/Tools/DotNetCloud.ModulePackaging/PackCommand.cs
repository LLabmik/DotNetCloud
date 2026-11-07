using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using DotNetCloud.Core.Server.ModuleLoading;

namespace DotNetCloud.ModulePackaging;

/// <summary>The <c>pack</c> command.</summary>
internal static class PackCommand
{
    /// <summary>Creates the command.</summary>
    /// <returns>The configured command.</returns>
    public static Command Create()
    {
        var inputOption = new Option<string?>("--input")
        {
            Description = "Staging directory containing manifest.json and files/<rid>/…",
        };

        var projectOption = new Option<string?>("--project")
        {
            Description = "Module host project to publish with 'dotnet publish' (alternative to --input)",
        };

        var ridOption = new Option<string[]>("--rid")
        {
            Description = "Runtime identifier to publish (repeatable). Required with --project.",
            AllowMultipleArgumentsPerToken = true,
        };

        var configurationOption = new Option<string?>("--configuration")
        {
            Description = "Build configuration used by 'dotnet publish' (default: Release)",
        };

        var manifestOption = new Option<string?>("--manifest")
        {
            Description = "Manifest to embed (default: <input>/manifest.json or <projectDir>/../manifest.json)",
        };

        var outputOption = new Option<string?>("--output")
        {
            Description = "Package path (default: <id>-<version>.dncpkg in the current directory)",
        };

        var signKeyOption = new Option<string?>("--sign-key")
        {
            Description = "Key-pair JSON produced by 'dotnetcloud-pack keygen'",
        };

        var feedOption = new Option<string?>("--feed")
        {
            Description = "Also copy the package into this local feed and update its index.json",
        };

        var noBuildOption = new Option<bool>("--no-build")
        {
            Description = "Pass --no-build to 'dotnet publish'",
        };

        var command = new Command("pack", "Build a deterministic, signed .dncpkg package")
        {
            inputOption,
            projectOption,
            ridOption,
            configurationOption,
            manifestOption,
            outputOption,
            signKeyOption,
            feedOption,
            noBuildOption,
        };

        command.SetAction(parseResult => RunAsync(
            parseResult.GetValue(inputOption),
            parseResult.GetValue(projectOption),
            parseResult.GetValue(ridOption) ?? [],
            parseResult.GetValue(configurationOption) ?? "Release",
            parseResult.GetValue(manifestOption),
            parseResult.GetValue(outputOption),
            parseResult.GetValue(signKeyOption),
            parseResult.GetValue(feedOption),
            parseResult.GetValue(noBuildOption),
            CancellationToken.None));

        return command;
    }

    private static async Task<int> RunAsync(
        string? input,
        string? project,
        string[] rids,
        string configuration,
        string? manifest,
        string? output,
        string? signKey,
        string? feed,
        bool noBuild,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input) == string.IsNullOrWhiteSpace(project))
        {
            Program.Error("Specify exactly one of --input or --project.");
            return 1;
        }

        string? temporaryRoot = null;

        try
        {
            var staging = Path.GetFullPath(input ?? Path.Combine(
                Path.GetTempPath(), "dnc-pack-" + Guid.CreateVersion7().ToString("N")));

            if (project is not null)
            {
                if (rids.Length == 0)
                {
                    Program.Error("--project requires at least one --rid (for example linux-x64).");
                    return 1;
                }

                temporaryRoot = staging;
                Directory.CreateDirectory(staging);

                foreach (var rid in rids)
                {
                    var publishDirectory = Path.Combine(staging, ModulePackageFormat.FilesDirectoryName, rid);
                    var exitCode = await PublishAsync(project, rid, configuration, publishDirectory, noBuild, cancellationToken);
                    if (exitCode != 0)
                    {
                        Program.Error($"'dotnet publish' failed for {rid} with exit code {exitCode}.");
                        return 1;
                    }
                }
            }

            var manifestPath = ResolveManifestPath(manifest, staging, project);
            if (manifestPath is null)
            {
                Program.Error("No manifest.json found. Use --manifest <path> or place one in the staging directory.");
                return 1;
            }

            var stagedManifest = Path.Combine(staging, ModulePackageFormat.ManifestEntryName);
            if (!string.Equals(Path.GetFullPath(manifestPath), Path.GetFullPath(stagedManifest), StringComparison.Ordinal))
            {
                File.Copy(manifestPath, stagedManifest, overwrite: true);
            }

            CopyModuleDocuments(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!, staging);

            var manifestProblem = ValidateManifest(stagedManifest);
            if (manifestProblem is not null)
            {
                Program.Error(manifestProblem);
                return 1;
            }

            var (moduleId, version) = ReadModuleIdentity(stagedManifest);
            var packagePath = Path.GetFullPath(
                output ?? Path.Combine(Environment.CurrentDirectory, ModulePackageFormat.BuildFileName(moduleId, version)));

            var entries = DeterministicPackageWriter.CollectFromDirectory(staging);
            var build = await DeterministicPackageWriter.WriteAsync(packagePath, entries, cancellationToken);

            Program.Info($"Packed {build.EntryCount} entries -> {build.PackagePath}");
            Program.Info($"  sha256 {build.Sha256}");
            Program.Info($"  size   {build.SizeBytes} bytes");

            ModulePackageSignatureFile? signature = null;
            if (signKey is not null)
            {
                var keyPair = Ed25519Signature.ReadKeyPair(signKey);
                signature = await ModulePackageSigner.SignAsync(build.PackagePath, keyPair, cancellationToken: cancellationToken);
                Program.Info($"Signed with key '{signature.KeyId}' -> {ModulePackageFormat.GetSignaturePath(build.PackagePath)}");
            }
            else
            {
                Program.Warn("The package is unsigned. Sideloading requires --allow-unverified.");
            }

            var verification = await ModulePackageVerifier.VerifyAsync(
                build.PackagePath,
                signaturePath: null,
                new ModulePackageVerificationOptions
                {
                    TrustedKeys = signature is null ? new Dictionary<string, string>() : BuildKeys(signKey!),
                    AllowUnsigned = signature is null,
                },
                cancellationToken);

            Program.ReportIssues(verification);
            if (!verification.IsValid)
            {
                Program.Error("The package failed its own verification; nothing was published.");
                return 1;
            }

            if (feed is not null)
            {
                var feedRoot = Path.GetFullPath(feed);
                Directory.CreateDirectory(feedRoot);

                var feedPackagePath = Path.Combine(feedRoot, Path.GetFileName(build.PackagePath));
                if (!string.Equals(feedPackagePath, build.PackagePath, StringComparison.Ordinal))
                {
                    File.Copy(build.PackagePath, feedPackagePath, overwrite: true);

                    var signaturePath = ModulePackageFormat.GetSignaturePath(build.PackagePath);
                    if (File.Exists(signaturePath))
                    {
                        File.Copy(signaturePath, ModulePackageFormat.GetSignaturePath(feedPackagePath), overwrite: true);
                    }
                }

                var entry = await ModuleFeed.AddAsync(feedRoot, feedPackagePath, signature, cancellationToken);
                Program.Info($"Feed index updated: {entry.Id} {entry.Version} -> {Path.Combine(feedRoot, ModuleFeed.IndexFileName)}");
            }

            return 0;
        }
        catch (ModulePackageException ex)
        {
            Program.Error(ex.Message);
            foreach (var issue in ex.Issues)
            {
                Program.Error($"  {issue}");
            }

            return 1;
        }
        finally
        {
            if (temporaryRoot is not null && Directory.Exists(temporaryRoot))
            {
                try
                {
                    Directory.Delete(temporaryRoot, recursive: true);
                }
                catch (IOException)
                {
                    // Best effort.
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, string> BuildKeys(string signKeyPath)
    {
        var keyPair = Ed25519Signature.ReadKeyPair(signKeyPath);
        return new Dictionary<string, string>(StringComparer.Ordinal) { [keyPair.KeyId] = keyPair.PublicKey };
    }

    private static string? ResolveManifestPath(string? explicitPath, string staging, string? project)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        var staged = Path.Combine(staging, ModulePackageFormat.ManifestEntryName);
        if (File.Exists(staged))
        {
            return staged;
        }

        if (project is not null)
        {
            var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(project))!;
            var candidates = new[]
            {
                Path.Combine(projectDirectory, ModulePackageFormat.ManifestEntryName),
                Path.Combine(projectDirectory, "..", ModulePackageFormat.ManifestEntryName),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Copies LICENSE/README.md/CHANGELOG.md/icon.png from the manifest's directory into the
    /// staging root when they are present there.
    /// </summary>
    private static void CopyModuleDocuments(string manifestDirectory, string staging)
    {
        string[] documents =
        [
            ModulePackageFormat.LicenseEntryName,
            ModulePackageFormat.ReadmeEntryName,
            ModulePackageFormat.ChangelogEntryName,
            ModulePackageFormat.IconEntryName,
        ];

        foreach (var document in documents)
        {
            var source = Path.Combine(manifestDirectory, document);
            var target = Path.Combine(staging, document);

            if (File.Exists(source)
                && !string.Equals(Path.GetFullPath(source), Path.GetFullPath(target), StringComparison.Ordinal))
            {
                File.Copy(source, target, overwrite: true);
            }
        }
    }

    private static string? ValidateManifest(string manifestPath)
    {
        var loader = new ModuleManifestLoader(NullLogger<ModuleManifestLoader>.Instance);
        var expectedId = Path.GetFileName(Path.GetDirectoryName(manifestPath)!);

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        if (document.RootElement.TryGetProperty("id", out var idElement)
            && idElement.ValueKind == JsonValueKind.String)
        {
            expectedId = idElement.GetString() ?? expectedId;
        }

        var result = loader.LoadAndValidate(manifestPath, expectedId);
        if (result.IsValid)
        {
            return null;
        }

        return "manifest.json failed validation:" + Environment.NewLine +
               string.Join(Environment.NewLine, result.Errors.Select(error => $"  {error}"));
    }

    private static (string ModuleId, string Version) ReadModuleIdentity(string manifestPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var id = document.RootElement.TryGetProperty("id", out var idElement)
            ? idElement.GetString() ?? string.Empty
            : string.Empty;

        var version = document.RootElement.TryGetProperty("version", out var versionElement)
            ? versionElement.GetString() ?? string.Empty
            : string.Empty;

        if (id.Length == 0 || version.Length == 0)
        {
            throw new ModulePackageException("The manifest must declare both 'id' and 'version'.");
        }

        return (id, version);
    }

    private static async Task<int> PublishAsync(
        string project,
        string rid,
        string configuration,
        string outputDirectory,
        bool noBuild,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "publish",
            Path.GetFullPath(project),
            "-c",
            configuration,
            "-r",
            rid,
            "--self-contained",
            "false",
            "-o",
            outputDirectory,
            "-nologo",
        };

        if (noBuild)
        {
            arguments.Add("--no-build");
        }

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new ModulePackageException("Could not start 'dotnet publish'.");

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            Program.Error(await standardError);
            Program.Error(await standardOutput);
        }

        return process.ExitCode;
    }
}
