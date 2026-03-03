using System.Text.Json;
using DotNetCloud.Core.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.ModuleLoading;

/// <summary>
/// Loads module-specific configuration from multiple sources:
/// 1. Module's appsettings.json file (if present)
/// 2. System settings (SystemSetting table)
/// 3. Module settings (database ModuleSettings)
/// </summary>
/// <remarks>
/// Configuration precedence (highest to lowest):
/// 1. Database module-specific settings (IModuleSettings)
/// 2. Module's appsettings.json file
/// 3. System defaults
/// </remarks>
internal sealed class ModuleConfigurationLoader
{
    private readonly ILogger<ModuleConfigurationLoader> _logger;
    private readonly CoreDbContext _dbContext;
    private readonly IConfiguration _coreConfiguration;

    public ModuleConfigurationLoader(
        ILogger<ModuleConfigurationLoader> logger,
        CoreDbContext dbContext,
        IConfiguration coreConfiguration)
    {
        _logger = logger;
        _dbContext = dbContext;
        _coreConfiguration = coreConfiguration;
    }

    /// <summary>
    /// Loads configuration for a module from all available sources.
    /// </summary>
    /// <param name="moduleId">The module identifier.</param>
    /// <param name="moduleDirectory">The module's installation directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of configuration key-value pairs.</returns>
    public async Task<IReadOnlyDictionary<string, string>> LoadConfigurationAsync(
        string moduleId,
        string moduleDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        ArgumentNullException.ThrowIfNull(moduleDirectory);

        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Load from module's appsettings.json (if exists)
        var fileConfig = LoadFromAppSettingsFile(moduleId, moduleDirectory);
        foreach (var kvp in fileConfig)
        {
            config[kvp.Key] = kvp.Value;
        }

        // 2. Load from database system settings for this module
        var systemSettings = await LoadSystemSettingsAsync(moduleId, cancellationToken);
        foreach (var kvp in systemSettings)
        {
            config[kvp.Key] = kvp.Value; // Overwrites file config
        }

        // 3. Load from core configuration (environment-specific overrides)
        var coreSection = _coreConfiguration.GetSection($"Modules:{moduleId}");
        if (coreSection.Exists())
        {
            foreach (var kvp in coreSection.AsEnumerable(makePathsRelative: true))
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    config[kvp.Key] = kvp.Value;
                }
            }
        }

        _logger.LogInformation(
            "Loaded {Count} configuration values for module {ModuleId}",
            config.Count, moduleId);

        return config;
    }

    /// <summary>
    /// Loads configuration from a module's appsettings.json file.
    /// </summary>
    private Dictionary<string, string> LoadFromAppSettingsFile(string moduleId, string moduleDirectory)
    {
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var appSettingsPath = Path.Combine(moduleDirectory, "appsettings.json");

        if (!File.Exists(appSettingsPath))
        {
            _logger.LogDebug("No appsettings.json found for module {ModuleId} at {Path}",
                moduleId, appSettingsPath);
            return config;
        }

        try
        {
            var json = File.ReadAllText(appSettingsPath);
            var doc = JsonDocument.Parse(json);

            FlattenJsonToConfig(doc.RootElement, string.Empty, config);

            _logger.LogDebug(
                "Loaded {Count} settings from {Path} for module {ModuleId}",
                config.Count, appSettingsPath, moduleId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to parse appsettings.json for module {ModuleId} at {Path}",
                moduleId, appSettingsPath);
        }

        return config;
    }

    /// <summary>
    /// Loads system-wide settings for a module from the database.
    /// </summary>
    private async Task<Dictionary<string, string>> LoadSystemSettingsAsync(
        string moduleId,
        CancellationToken cancellationToken)
    {
        var settings = await _dbContext.SystemSettings
            .Where(s => s.Module == moduleId)
            .ToListAsync(cancellationToken);

        var config = settings.ToDictionary(
            s => s.Key,
            s => s.Value,
            StringComparer.OrdinalIgnoreCase);

        _logger.LogDebug(
            "Loaded {Count} system settings for module {ModuleId}",
            config.Count, moduleId);

        return config;
    }

    /// <summary>
    /// Flattens a JSON document into a configuration key-value dictionary.
    /// Nested objects become colon-separated keys (e.g., "Database:ConnectionString").
    /// </summary>
    private static void FlattenJsonToConfig(
        JsonElement element,
        string prefix,
        Dictionary<string, string> config)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix)
                        ? property.Name
                        : $"{prefix}:{property.Name}";
                    FlattenJsonToConfig(property.Value, key, config);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var key = $"{prefix}:{index}";
                    FlattenJsonToConfig(item, key, config);
                    index++;
                }
                break;

            case JsonValueKind.String:
                config[prefix] = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number:
                config[prefix] = element.GetRawText();
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                config[prefix] = element.GetBoolean().ToString();
                break;

            case JsonValueKind.Null:
                config[prefix] = string.Empty;
                break;
        }
    }

    /// <summary>
    /// Gets the gRPC endpoint for the core server that modules should connect to.
    /// </summary>
    /// <returns>The core gRPC endpoint address.</returns>
    public string GetCoreGrpcEndpoint()
    {
        // This will be configured based on the transport type (Unix socket, Named Pipe, TCP)
        var endpoint = _coreConfiguration["Grpc:CoreEndpoint"];

        if (!string.IsNullOrEmpty(endpoint))
        {
            return endpoint;
        }

        // Default: will be set by GrpcServerConfiguration
        return "unix:///run/dotnetcloud/core.sock";
    }
}
