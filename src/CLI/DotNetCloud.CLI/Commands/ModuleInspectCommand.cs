using System.CommandLine;
using System.IO.Compression;
using System.Text.Json;
using DotNetCloud.ModulePackaging;

namespace DotNetCloud.CLI.Commands;

/// <summary>The <c>module inspect</c> command.</summary>
internal static class ModuleInspectCommand
{
    /// <summary>Creates the command.</summary>
    /// <returns>The configured command.</returns>
    public static Command Create()
    {
        var packageArgument = new Argument<string>("package")
        {
            Description = "The .dncpkg file to inspect",
        };

        var signatureOption = new Option<string?>("--signature")
        {
            Description = "Detached signature file (default: <package>.sig, when present)",
        };

        var command = new Command("inspect", "Print a package's manifest, entries and signature state")
        {
            packageArgument,
            signatureOption,
        };

        command.SetAction(parseResult => RunAsync(
            parseResult.GetValue(packageArgument)!,
            parseResult.GetValue(signatureOption)));

        return command;
    }

    private static async Task<int> RunAsync(string package, string? signature)
    {
        if (!File.Exists(package))
        {
            ModulePackageConsole.Error($"Package not found: {package}");
            return 1;
        }

        try
        {
            using var archive = ZipFile.OpenRead(package);

            ModulePackageConsole.Info($"package : {Path.GetFullPath(package)}");
            ModulePackageConsole.Info($"size    : {new FileInfo(package).Length} bytes");
            ModulePackageConsole.Info($"sha256  : {await DeterministicPackageWriter.ComputeSha256Async(package)}");

            var manifestEntry = archive.GetEntry(ModulePackageFormat.ManifestEntryName);
            if (manifestEntry is not null)
            {
                using var reader = new StreamReader(manifestEntry.Open());
                using var document = JsonDocument.Parse(reader.ReadToEnd(), new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });

                var root = document.RootElement;
                ModulePackageConsole.Info(string.Empty);
                ModulePackageConsole.Info("manifest");
                ModulePackageConsole.Info($"  id               {ReadString(root, "id")}");
                ModulePackageConsole.Info($"  name             {ReadString(root, "name")}");
                ModulePackageConsole.Info($"  version          {ReadString(root, "version")}");
                ModulePackageConsole.Info($"  manifestVersion  {(root.TryGetProperty("manifestVersion", out var manifestVersion) ? manifestVersion.ToString() : "1")}");
                ModulePackageConsole.Info($"  dataStore        {ReadString(root, "dataStore") ?? "none"}");
                ModulePackageConsole.Info($"  minCoreVersion   {ReadString(root, "minCoreVersion") ?? "(none)"}");
                ModulePackageConsole.Info($"  moduleApiVersion {(root.TryGetProperty("moduleApiVersion", out var apiVersion) ? apiVersion.ToString() : "(none)")}");

                if (root.TryGetProperty("publisher", out var publisher) && publisher.ValueKind == JsonValueKind.Object)
                {
                    ModulePackageConsole.Info($"  publisher        {ReadString(publisher, "id")} ({ReadString(publisher, "name")})");
                }

                var capabilities = ReadArray(root, "capabilities", "requiredCapabilities");
                if (capabilities.Count > 0)
                {
                    ModulePackageConsole.Info($"  capabilities     {string.Join(", ", capabilities)}");
                }

                var scopes = ReadArray(root, "userConsentScopes");
                if (scopes.Count > 0)
                {
                    ModulePackageConsole.Info($"  consent scopes   {string.Join(", ", scopes)}");
                }

                if (root.TryGetProperty("handlesUserErasure", out var handles))
                {
                    ModulePackageConsole.Info($"  erasure          {handles}");
                }
            }

            ModulePackageConsole.Info(string.Empty);
            ModulePackageConsole.Info("entries");

            foreach (var entry in archive.Entries.OrderBy(entry => entry.FullName, StringComparer.Ordinal))
            {
                var marker = entry.FullName.EndsWith('/') ? "dir" : entry.Length.ToString();
                ModulePackageConsole.Info($"  {entry.FullName}  ({marker})");
            }

            var signaturePath = signature ?? ModulePackageFormat.GetSignaturePath(package);
            ModulePackageConsole.Info(string.Empty);

            if (File.Exists(signaturePath))
            {
                var parsed = ModulePackageSigner.Read(signaturePath);
                ModulePackageConsole.Info($"signature: key '{parsed.KeyId}', sha256 {parsed.Sha256}");
            }
            else
            {
                ModulePackageConsole.Info("signature: none");
            }

            var safety = ArchiveSafetyInspector.Inspect(ArchiveSafetyInspector.Describe(archive));
            foreach (var issue in safety)
            {
                ModulePackageConsole.Error(issue.ToString());
            }

            return safety.Count == 0 ? 0 : 1;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ModulePackageException)
        {
            ModulePackageConsole.Error(ex.Message);
            return 1;
        }
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> ReadArray(JsonElement element, params string[] properties)
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
}
