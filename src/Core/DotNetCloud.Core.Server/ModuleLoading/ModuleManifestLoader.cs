using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.ModuleLoading;

/// <summary>
/// Manifest data loaded from a module's manifest.json file.
/// </summary>
/// <remarks>
/// Version 1 manifests (required and bundled first-party modules) keep the original lenient model.
/// Version 2 manifests (sandboxed modules) add publisher, data-store, consent, UI and quota fields
/// and are parsed strictly — see <see cref="ModuleManifestLoader"/>.
/// </remarks>
internal sealed record ModuleManifestData
{
    /// <summary>Manifest schema version. Defaults to 1 for backwards compatibility.</summary>
    public int ManifestVersion { get; init; } = 1;

    /// <summary>Module identifier (e.g., "dotnetcloud.files").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable module name.</summary>
    public required string Name { get; init; }

    /// <summary>Semantic version string.</summary>
    public required string Version { get; init; }

    /// <summary>Module description.</summary>
    public string? Description { get; init; }

    /// <summary>Module author or publisher (v1 free text).</summary>
    public string? Author { get; init; }

    /// <summary>Publisher identity (v2).</summary>
    public ManifestPublisher? Publisher { get; init; }

    /// <summary>Required capability interface names (v1 field).</summary>
    public IReadOnlyList<string> RequiredCapabilities { get; init; } = [];

    /// <summary>Required capability interface names (v2 field, preferred when present).</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>Event types published by this module (v1 field).</summary>
    public IReadOnlyList<string> PublishedEvents { get; init; } = [];

    /// <summary>Event types this module subscribes to (v1 field).</summary>
    public IReadOnlyList<string> SubscribedEvents { get; init; } = [];

    /// <summary>Event declarations (v2 field).</summary>
    public ManifestEvents? Events { get; init; }

    /// <summary>Real-time (SignalR) event names the module may broadcast (v1 field).</summary>
    public IReadOnlyList<string> RealtimeEvents { get; init; } = [];

    /// <summary>OIDC scopes the module needs per-user consent for (v2).</summary>
    public IReadOnlyList<string> UserConsentScopes { get; init; } = [];

    /// <summary>How the module persists its own data: <c>isolated</c> or <c>none</c> (v2).</summary>
    public string? DataStore { get; init; }

    /// <summary>Whether the module implements the core-to-module user-erasure contract (v2).</summary>
    public bool? HandlesUserErasure { get; init; }

    /// <summary>Module UI descriptor (v2).</summary>
    public ManifestUi? Ui { get; init; }

    /// <summary>Resource requests (v2). Requests are capped by admin/store policy, never entitlements.</summary>
    public ManifestResources? Resources { get; init; }

    /// <summary>
    /// Minimum core version this module is compatible with. Enforced by
    /// <see cref="ModuleCompatibility"/> before the module process is started.
    /// </summary>
    public string? MinCoreVersion { get; init; }

    /// <summary>Maximum core version this module is compatible with (v2).</summary>
    public string? MaxCoreVersion { get; init; }

    /// <summary>Module API version the module is built against (v2).</summary>
    public int? ModuleApiVersion { get; init; }

    /// <summary>Configured restart policy override, or null for default.</summary>
    public string? RestartPolicy { get; init; }

    /// <summary>Memory limit in MB override, or null for default (v1).</summary>
    public int? MemoryLimitMb { get; init; }

    /// <summary>Capabilities the module requests, whichever manifest version declared them.</summary>
    public IReadOnlyList<string> EffectiveCapabilities =>
        ManifestVersion >= 2 ? Capabilities : RequiredCapabilities;

    /// <summary>Events the module publishes, whichever manifest version declared them.</summary>
    public IReadOnlyList<string> EffectivePublishedEvents =>
        ManifestVersion >= 2 && Events is not null ? Events.Publishes : PublishedEvents;

    /// <summary>Events the module subscribes to, whichever manifest version declared them.</summary>
    public IReadOnlyList<string> EffectiveSubscribedEvents =>
        ManifestVersion >= 2 && Events is not null ? Events.Subscribes : SubscribedEvents;

    /// <summary>
    /// Real-time event names the module may broadcast. Empty means "not declared", which the core
    /// treats as unenforced for backwards compatibility while the vocabulary is rolled out.
    /// </summary>
    public IReadOnlyList<string> EffectiveRealtimeEvents =>
        ManifestVersion >= 2 && Events is not null ? Events.Realtime : RealtimeEvents;

    /// <summary>Resolved data-store mode.</summary>
    public ModuleDataStoreMode EffectiveDataStoreMode =>
        string.Equals(DataStore, "isolated", StringComparison.OrdinalIgnoreCase)
            ? ModuleDataStoreMode.Isolated
            : ModuleDataStoreMode.None;

    /// <summary>Effective memory request, preferring the v2 resources block.</summary>
    public int? EffectiveMemoryLimitMb => Resources?.MemoryLimitMb ?? MemoryLimitMb;
}

/// <summary>Publisher identity from a v2 manifest.</summary>
internal sealed record ManifestPublisher
{
    /// <summary>Stable publisher id (reverse-DNS or slug).</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public string? Name { get; init; }

    /// <summary>Publisher home page (<c>https:</c> only).</summary>
    public string? Url { get; init; }
}

/// <summary>Event declarations from a v2 manifest.</summary>
internal sealed record ManifestEvents
{
    /// <summary>Event types the module publishes.</summary>
    public IReadOnlyList<string> Publishes { get; init; } = [];

    /// <summary>Event types the module subscribes to.</summary>
    public IReadOnlyList<string> Subscribes { get; init; } = [];

    /// <summary>Real-time (SignalR) event names the module may broadcast.</summary>
    public IReadOnlyList<string> Realtime { get; init; } = [];
}

/// <summary>Module UI descriptor from a v2 manifest.</summary>
internal sealed record ManifestUi
{
    /// <summary>UI hosting type. Only <c>iframe</c> is supported.</summary>
    public string Type { get; init; } = "iframe";

    /// <summary>Path the module serves its UI at.</summary>
    public string? EntryPath { get; init; }

    /// <summary>Navigation entries contributed to the core chrome.</summary>
    public IReadOnlyList<ManifestNavItem> NavItems { get; init; } = [];
}

/// <summary>Navigation entry from a v2 manifest.</summary>
internal sealed record ManifestNavItem
{
    /// <summary>Stable navigation id.</summary>
    public required string Id { get; init; }

    /// <summary>Display label.</summary>
    public required string Label { get; init; }

    /// <summary>Google Material Icons ligature name; the core maps it through <c>ModuleIconProvider</c>.</summary>
    public string? Icon { get; init; }

    /// <summary>Module-relative path to navigate to.</summary>
    public string? Path { get; init; }
}

/// <summary>Resource requests from a v2 manifest.</summary>
internal sealed record ManifestResources
{
    /// <summary>Requested memory ceiling in megabytes.</summary>
    public int? MemoryLimitMb { get; init; }

    /// <summary>Requested CPU share as a percentage of one core.</summary>
    public int? CpuPercent { get; init; }

    /// <summary>Requested data-directory ceiling in megabytes.</summary>
    public int? DataDirMb { get; init; }

    /// <summary>Requested database-schema ceiling in megabytes.</summary>
    public int? DatabaseMb { get; init; }

    /// <summary>Requested database connection limit.</summary>
    public int? ConnectionLimit { get; init; }
}

/// <summary>
/// Result of manifest validation.
/// </summary>
internal sealed record ManifestValidationResult
{
    /// <summary>Whether the manifest is valid.</summary>
    public bool IsValid { get; init; }

    /// <summary>Validation error messages.</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>The validated manifest data, or null if invalid.</summary>
    public ModuleManifestData? Manifest { get; init; }

    public static ManifestValidationResult Success(ModuleManifestData manifest)
        => new() { IsValid = true, Manifest = manifest };

    public static ManifestValidationResult Failure(params string[] errors)
        => new() { IsValid = false, Errors = errors };
}

/// <summary>
/// Loads and validates module manifests from manifest.json files.
/// </summary>
/// <remarks>
/// <para>
/// Version 1 manifests stay lenient (case-insensitive property names, comments and trailing commas
/// allowed) so required-module compatibility is preserved. Version 2 manifests are parsed strictly:
/// unknown top-level fields are an error, and comments/trailing commas are rejected.
/// </para>
/// <para>
/// The manifest is untrusted input in both cases. Shape validation here covers syntax and
/// self-consistency; policy (which capabilities an operator approves, which quotas are effective)
/// is applied at install time and by the supervisor.
/// </para>
/// </remarks>
internal sealed class ModuleManifestLoader
{
    private const int MaxModuleIdLength = 200;
    private const int MaxModuleIdLabelLength = 63;
    private const int SupportedModuleApiVersion = 1;

    private static readonly Regex ModuleIdPattern = new(
        "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MaterialIconPattern = new(
        "^[a-z][a-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ConsentScopePattern = new(
        "^[a-z][a-z0-9-]*:(read|write)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly ILogger<ModuleManifestLoader> _logger;

    private static readonly JsonSerializerOptions LenientJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions StrictJsonOptions = new()
    {
        // v2 manifests are camelCase (the documented contract) and must not rely on
        // case-insensitive matching to hide typos.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public ModuleManifestLoader(ILogger<ModuleManifestLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads and validates a module manifest from the given file path.
    /// </summary>
    /// <param name="manifestPath">Path to the manifest.json file.</param>
    /// <param name="expectedModuleId">The expected module ID (from directory name).</param>
    /// <returns>The validation result containing the manifest or errors.</returns>
    public ManifestValidationResult LoadAndValidate(string manifestPath, string expectedModuleId)
    {
        ArgumentNullException.ThrowIfNull(manifestPath);
        ArgumentNullException.ThrowIfNull(expectedModuleId);

        if (!File.Exists(manifestPath))
        {
            return ManifestValidationResult.Failure($"Manifest file not found: {manifestPath}");
        }

        ModuleManifestData manifest;
        try
        {
            var json = File.ReadAllText(manifestPath);
            var isV2 = PeekManifestVersion(json) >= 2;
            var options = isV2 ? StrictJsonOptions : LenientJsonOptions;

            manifest = JsonSerializer.Deserialize<ModuleManifestData>(json, options)
                ?? throw new JsonException("Deserialization returned null");

            // The published v2 example uses "backoff"; the runtime enum calls it
            // ExponentialBackoff. Normalize once here so both spellings mean the same thing.
            if (manifest.ManifestVersion >= 2
                && string.Equals(manifest.RestartPolicy, "backoff", StringComparison.OrdinalIgnoreCase))
            {
                manifest = manifest with { RestartPolicy = nameof(Core.Modules.Supervisor.RestartPolicy.ExponentialBackoff) };
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse manifest.json at {Path}", manifestPath);
            return ManifestValidationResult.Failure($"Invalid JSON in manifest: {ex.Message}");
        }

        return Validate(manifest, expectedModuleId);
    }

    /// <summary>
    /// Validates a loaded manifest against the module manifest rules.
    /// </summary>
    /// <param name="manifest">The manifest data to validate.</param>
    /// <param name="expectedModuleId">The expected module ID from the directory name.</param>
    /// <returns>The validation result.</returns>
    internal ManifestValidationResult Validate(ModuleManifestData manifest, string expectedModuleId)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var errors = new List<string>();

        if (manifest.ManifestVersion is < 1 or > 2)
        {
            errors.Add($"Unsupported manifestVersion {manifest.ManifestVersion}. Supported: 1, 2.");
        }

        // Required fields
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            errors.Add("Module ID is required.");
        }
        else if (manifest.ManifestVersion >= 2)
        {
            ValidateStrictModuleId(manifest.Id, expectedModuleId, errors);
        }
        else
        {
            ValidateLegacyModuleId(manifest.Id, expectedModuleId, errors);
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            errors.Add("Module name is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            errors.Add("Module version is required.");
        }
        else if (!System.Version.TryParse(manifest.Version.Split('-')[0], out _))
        {
            errors.Add($"Module version must follow semantic versioning. Got: '{manifest.Version}'");
        }

        // A malformed compatibility floor would silently disable the start-time check.
        if (!string.IsNullOrWhiteSpace(manifest.MinCoreVersion)
            && !ModuleCompatibility.TryParseVersion(manifest.MinCoreVersion, out _))
        {
            errors.Add($"minCoreVersion must be a semantic version. Got: '{manifest.MinCoreVersion}'");
        }

        if (!string.IsNullOrWhiteSpace(manifest.MaxCoreVersion)
            && !ModuleCompatibility.TryParseVersion(manifest.MaxCoreVersion, out _))
        {
            errors.Add($"maxCoreVersion must be a semantic version. Got: '{manifest.MaxCoreVersion}'");
        }

        // Validate restart policy if specified
        if (!string.IsNullOrEmpty(manifest.RestartPolicy) &&
            !Enum.TryParse<Core.Modules.Supervisor.RestartPolicy>(manifest.RestartPolicy, ignoreCase: true, out _))
        {
            errors.Add(
                $"Invalid restart policy '{manifest.RestartPolicy}'. " +
                $"Valid values: {string.Join(", ", Enum.GetNames<Core.Modules.Supervisor.RestartPolicy>())}");
        }

        // Validate memory limit
        if (manifest.MemoryLimitMb.HasValue && manifest.MemoryLimitMb.Value <= 0)
        {
            errors.Add($"Memory limit must be positive. Got: {manifest.MemoryLimitMb.Value}");
        }

        if (manifest.ManifestVersion >= 2)
        {
            ValidateSandboxFields(manifest, errors);
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning(
                "Manifest validation failed for {ModuleId}: {Errors}",
                manifest.Id ?? expectedModuleId,
                string.Join("; ", errors));

            return ManifestValidationResult.Failure([.. errors]);
        }

        _logger.LogDebug("Manifest validated for {ModuleId} v{Version}", manifest.Id, manifest.Version);
        return ManifestValidationResult.Success(manifest);
    }

    private static void ValidateLegacyModuleId(string id, string expectedModuleId, List<string> errors)
    {
        if (id != id.ToLowerInvariant())
        {
            errors.Add($"Module ID must be lowercase. Got: '{id}'");
        }

        if (!id.Contains('.'))
        {
            errors.Add($"Module ID must be dot-separated (e.g., 'dotnetcloud.files'). Got: '{id}'");
        }

        if (!string.Equals(id, expectedModuleId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Module ID '{id}' does not match directory name '{expectedModuleId}'.");
        }
    }

    private static void ValidateStrictModuleId(string id, string expectedModuleId, List<string> errors)
    {
        if (id.Length > MaxModuleIdLength)
        {
            errors.Add($"Module ID must be at most {MaxModuleIdLength} characters. Got {id.Length}.");
        }

        if (id.Split('.').Any(label => label.Length > MaxModuleIdLabelLength))
        {
            errors.Add(
                $"Each module ID label must be at most {MaxModuleIdLabelLength} characters (PostgreSQL identifier limit).");
        }

        if (!ModuleIdPattern.IsMatch(id))
        {
            errors.Add(
                $"Module ID must be a lowercase reverse-DNS name of [a-z0-9-] labels. Got: '{id}'");
        }

        if (!string.Equals(id, expectedModuleId, StringComparison.Ordinal))
        {
            errors.Add($"Module ID '{id}' does not match directory name '{expectedModuleId}'.");
        }
    }

    private static void ValidateSandboxFields(ModuleManifestData manifest, List<string> errors)
    {
        if (manifest.ModuleApiVersion is { } apiVersion && apiVersion != SupportedModuleApiVersion)
        {
            errors.Add(
                $"Unsupported moduleApiVersion {apiVersion}. This core supports {SupportedModuleApiVersion}.");
        }

        if (manifest.DataStore is { } dataStore
            && !string.Equals(dataStore, "isolated", StringComparison.Ordinal)
            && !string.Equals(dataStore, "none", StringComparison.Ordinal))
        {
            errors.Add($"dataStore must be 'isolated' or 'none'. Got: '{dataStore}'");
        }

        // Required modules are core parts: they keep the shared schema and in-core UI.
        if (RequiredModules.IsRequired(manifest.Id))
        {
            if (manifest.DataStore is not null)
            {
                errors.Add("Required modules must not declare a dataStore.");
            }

            if (manifest.Ui is not null)
            {
                errors.Add("Required modules must not declare a UI (their pages live in the core).");
            }

            return;
        }

        if (manifest.Publisher is { } publisher)
        {
            if (string.IsNullOrWhiteSpace(publisher.Id))
            {
                errors.Add("publisher.id is required when a publisher block is present.");
            }

            if (!string.IsNullOrWhiteSpace(publisher.Url)
                && (!Uri.TryCreate(publisher.Url, UriKind.Absolute, out var publisherUri)
                    || publisherUri.Scheme != Uri.UriSchemeHttps))
            {
                errors.Add("publisher.url must be an absolute https URL.");
            }
        }

        foreach (var scope in manifest.UserConsentScopes)
        {
            if (!ConsentScopePattern.IsMatch(scope))
            {
                errors.Add($"Invalid userConsentScope '{scope}'. Expected '<resource>:read|write'.");
            }
        }

        var hasUserData = manifest.EffectiveDataStoreMode != ModuleDataStoreMode.None
            || manifest.UserConsentScopes.Count > 0;

        if (hasUserData && manifest.HandlesUserErasure != true)
        {
            errors.Add(
                "handlesUserErasure must be true when the module stores user data (dataStore != 'none' or userConsentScopes declared).");
        }

        if (manifest.Ui is { } ui)
        {
            if (!string.Equals(ui.Type, "iframe", StringComparison.Ordinal))
            {
                errors.Add($"ui.type must be 'iframe' for sandboxed modules. Got: '{ui.Type}'");
            }

            if (!string.IsNullOrEmpty(ui.EntryPath) && !ui.EntryPath.StartsWith('/'))
            {
                errors.Add("ui.entryPath must start with '/'.");
            }

            foreach (var navItem in ui.NavItems)
            {
                if (string.IsNullOrWhiteSpace(navItem.Id) || string.IsNullOrWhiteSpace(navItem.Label))
                {
                    errors.Add("ui.navItems entries require both id and label.");
                }

                if (!string.IsNullOrWhiteSpace(navItem.Icon) && !MaterialIconPattern.IsMatch(navItem.Icon))
                {
                    errors.Add(
                        $"ui.navItems '{navItem.Id}' icon must be a Material icon name ([a-z0-9_]). Got: '{navItem.Icon}'");
                }

                if (!string.IsNullOrEmpty(navItem.Path) && !navItem.Path.StartsWith('/'))
                {
                    errors.Add($"ui.navItems '{navItem.Id}' path must start with '/'.");
                }
            }
        }

        if (manifest.Resources is { } resources)
        {
            foreach (var (name, value) in new (string Name, int? Value)[]
                     {
                         (nameof(resources.MemoryLimitMb), resources.MemoryLimitMb),
                         (nameof(resources.CpuPercent), resources.CpuPercent),
                         (nameof(resources.DataDirMb), resources.DataDirMb),
                         (nameof(resources.DatabaseMb), resources.DatabaseMb),
                         (nameof(resources.ConnectionLimit), resources.ConnectionLimit),
                     })
            {
                if (value is <= 0)
                {
                    errors.Add($"resources.{name} must be positive when present. Got: {value}");
                }
            }
        }
    }

    /// <summary>
    /// Reads <c>manifestVersion</c> without committing to either parsing mode.
    /// </summary>
    private static int PeekManifestVersion(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && (document.RootElement.TryGetProperty("manifestVersion", out var version)
                    || document.RootElement.TryGetProperty("ManifestVersion", out version))
                && version.ValueKind == JsonValueKind.Number
                && version.TryGetInt32(out var value))
            {
                return value;
            }
        }
        catch (JsonException)
        {
            // Fall through: the real parse below reports the error.
        }

        return 1;
    }

    /// <summary>
    /// Creates a default manifest for a module that has no manifest.json file.
    /// Uses the module directory name as the ID and sets version to "0.0.0".
    /// </summary>
    /// <param name="moduleId">The module identifier from the directory name.</param>
    /// <returns>A default manifest.</returns>
    public ModuleManifestData CreateDefaultManifest(string moduleId)
    {
        _logger.LogWarning(
            "No manifest.json for module {ModuleId}, using default manifest",
            moduleId);

        return new ModuleManifestData
        {
            Id = moduleId,
            Name = moduleId,
            Version = "0.0.0",
            Description = "Module with no manifest.json"
        };
    }
}
