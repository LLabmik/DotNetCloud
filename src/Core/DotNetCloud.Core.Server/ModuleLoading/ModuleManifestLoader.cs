using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.ModuleLoading;

/// <summary>
/// Manifest data loaded from a module's manifest.json file.
/// </summary>
internal sealed record ModuleManifestData
{
    /// <summary>Module identifier (e.g., "dotnetcloud.files").</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable module name.</summary>
    public required string Name { get; init; }

    /// <summary>Semantic version string.</summary>
    public required string Version { get; init; }

    /// <summary>Module description.</summary>
    public string? Description { get; init; }

    /// <summary>Module author or publisher.</summary>
    public string? Author { get; init; }

    /// <summary>Required capability interface names.</summary>
    public IReadOnlyList<string> RequiredCapabilities { get; init; } = [];

    /// <summary>Event types published by this module.</summary>
    public IReadOnlyList<string> PublishedEvents { get; init; } = [];

    /// <summary>Event types this module subscribes to.</summary>
    public IReadOnlyList<string> SubscribedEvents { get; init; } = [];

    /// <summary>Minimum core version this module is compatible with.</summary>
    public string? MinCoreVersion { get; init; }

    /// <summary>Configured restart policy override, or null for default.</summary>
    public string? RestartPolicy { get; init; }

    /// <summary>Memory limit in MB override, or null for default.</summary>
    public int? MemoryLimitMb { get; init; }

    /// <summary>
    /// How the module's database schema is managed.
    /// "core" = core server runs migrations. "self" = module process self-migrates.
    /// Defaults to "self" when absent.
    /// </summary>
    public string SchemaProvider { get; init; } = "self";
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
internal sealed class ModuleManifestLoader
{
    private readonly ILogger<ModuleManifestLoader> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
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
            manifest = JsonSerializer.Deserialize<ModuleManifestData>(json, JsonOptions)
                ?? throw new JsonException("Deserialization returned null");
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
        var errors = new List<string>();

        // Required fields
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            errors.Add("Module ID is required.");
        }
        else
        {
            // ID format: lowercase, dot-separated
            if (manifest.Id != manifest.Id.ToLowerInvariant())
            {
                errors.Add($"Module ID must be lowercase. Got: '{manifest.Id}'");
            }

            if (!manifest.Id.Contains('.'))
            {
                errors.Add($"Module ID must be dot-separated (e.g., 'dotnetcloud.files'). Got: '{manifest.Id}'");
            }

            // ID must match directory name
            if (!string.Equals(manifest.Id, expectedModuleId, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"Module ID '{manifest.Id}' does not match directory name '{expectedModuleId}'.");
            }
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
