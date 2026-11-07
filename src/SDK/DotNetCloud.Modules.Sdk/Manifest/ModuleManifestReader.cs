using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Sdk.Manifest;

/// <summary>Outcome of reading and validating a module manifest.</summary>
/// <param name="Manifest">The parsed manifest, when valid.</param>
/// <param name="Errors">Human-readable validation errors.</param>
public sealed record ModuleManifestReadResult(ModuleManifestV2? Manifest, IReadOnlyList<string> Errors)
{
    /// <summary>Gets a value indicating whether the manifest is valid.</summary>
    public bool IsValid => Manifest is not null && Errors.Count == 0;
}

/// <summary>
/// Reads and validates a version 2 module manifest.
/// </summary>
/// <remarks>
/// Parsing is deliberately strict: unknown members, comments and trailing commas are errors. The
/// core applies the same rules when it loads the manifest at install time, so a manifest that
/// validates here will not be rejected there for a shape reason.
/// </remarks>
public static partial class ModuleManifestReader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly string[] RestartPolicies = ["always", "on-failure", "never", "backoff"];

    /// <summary>Reads a manifest from JSON text.</summary>
    /// <param name="json">The manifest JSON.</param>
    /// <returns>The parsed manifest and any validation errors.</returns>
    public static ModuleManifestReadResult Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        ModuleManifestV2? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ModuleManifestV2>(json, Options);
        }
        catch (JsonException ex)
        {
            return new ModuleManifestReadResult(null, [$"Manifest is not valid JSON: {ex.Message}"]);
        }

        if (manifest is null)
        {
            return new ModuleManifestReadResult(null, ["Manifest is empty."]);
        }

        return new ModuleManifestReadResult(manifest, Validate(manifest));
    }

    /// <summary>Reads a manifest from a file.</summary>
    /// <param name="path">Path to the manifest file.</param>
    /// <returns>The parsed manifest and any validation errors.</returns>
    public static ModuleManifestReadResult ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return new ModuleManifestReadResult(null, [$"Manifest file '{path}' does not exist."]);
        }

        return Read(File.ReadAllText(path));
    }

    /// <summary>Validates an already-parsed manifest.</summary>
    /// <param name="manifest">The manifest to validate.</param>
    /// <returns>The validation errors; empty when the manifest is valid.</returns>
    public static IReadOnlyList<string> Validate(ModuleManifestV2 manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var errors = new List<string>();

        if (manifest.ManifestVersion != 2)
        {
            errors.Add($"manifestVersion must be 2 for a sandboxed module (found {manifest.ManifestVersion}).");
        }

        if (string.IsNullOrWhiteSpace(manifest.Id) || !ModuleIdPattern().IsMatch(manifest.Id) || manifest.Id.Length > 100)
        {
            errors.Add("id must be a lowercase reverse-DNS identifier (for example 'org.example.app'), at most 100 characters.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            errors.Add("name is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version) || !SemanticVersionPattern().IsMatch(manifest.Version))
        {
            errors.Add("version must be a semantic version (for example '1.2.0').");
        }

        if (manifest.ModuleApiVersion < 1)
        {
            errors.Add("moduleApiVersion must be at least 1.");
        }

        if (manifest.DataStore is not (ModuleDataStoreModes.Isolated or ModuleDataStoreModes.None))
        {
            errors.Add($"dataStore must be '{ModuleDataStoreModes.Isolated}' or '{ModuleDataStoreModes.None}' (found '{manifest.DataStore}').");
        }

        if (manifest.Publisher is not null)
        {
            if (string.IsNullOrWhiteSpace(manifest.Publisher.Id))
            {
                errors.Add("publisher.id is required when a publisher block is present.");
            }

            if (string.IsNullOrWhiteSpace(manifest.Publisher.Name))
            {
                errors.Add("publisher.name is required when a publisher block is present.");
            }
        }

        if (manifest.RestartPolicy is not null && !RestartPolicies.Contains(manifest.RestartPolicy, StringComparer.Ordinal))
        {
            errors.Add($"restartPolicy must be one of: {string.Join(", ", RestartPolicies)}.");
        }

        if (manifest.Ui is not null)
        {
            if (!string.Equals(manifest.Ui.Type, "iframe", StringComparison.Ordinal))
            {
                errors.Add("ui.type must be 'iframe' for a non-required module.");
            }

            if (string.IsNullOrWhiteSpace(manifest.Ui.EntryPath) || !manifest.Ui.EntryPath.StartsWith('/'))
            {
                errors.Add("ui.entryPath must be a site-relative path starting with '/'.");
            }

            foreach (var item in manifest.Ui.NavItems)
            {
                if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Label)
                    || string.IsNullOrWhiteSpace(item.Icon))
                {
                    errors.Add("ui.navItems entries need an id, a label and a Material icon name.");
                }
            }
        }

        foreach (var (name, value) in new (string, int?)[]
                 {
                     ("memoryLimitMb", manifest.Resources?.MemoryLimitMb),
                     ("cpuPercent", manifest.Resources?.CpuPercent),
                     ("dataDirMb", manifest.Resources?.DataDirMb),
                     ("databaseMb", manifest.Resources?.DatabaseMb),
                     ("connectionLimit", manifest.Resources?.ConnectionLimit),
                 })
        {
            if (value is <= 0)
            {
                errors.Add($"resources.{name} must be a positive number.");
            }
        }

        if (manifest.UserConsentScopes.Count > 0)
        {
            foreach (var scope in ModuleScopes.FindUnknown(manifest.UserConsentScopes))
            {
                errors.Add($"Unknown userConsentScope '{scope}'.");
            }

            var declared = new HashSet<string>(manifest.UserConsentScopes, StringComparer.Ordinal);
            foreach (var capability in manifest.Capabilities)
            {
                foreach (var required in ModuleCapabilityScopes.RequiredFor(capability))
                {
                    if (!declared.Contains(required))
                    {
                        errors.Add(
                            $"Capability '{capability}' exposes personal data and requires " +
                            $"userConsentScopes to include '{required}'.");
                    }
                }
            }
        }

        if (!manifest.HandlesUserErasure
            && (manifest.DataStore != ModuleDataStoreModes.None || manifest.UserConsentScopes.Count > 0))
        {
            errors.Add(
                "handlesUserErasure must be true when the module stores data or requests a user consent scope.");
        }

        return errors;
    }

    [GeneratedRegex("^[a-z0-9]+(\\.[a-z0-9][a-z0-9-]*)+$")]
    private static partial Regex ModuleIdPattern();

    [GeneratedRegex("^\\d+\\.\\d+\\.\\d+(-[0-9A-Za-z.-]+)?$")]
    private static partial Regex SemanticVersionPattern();
}
