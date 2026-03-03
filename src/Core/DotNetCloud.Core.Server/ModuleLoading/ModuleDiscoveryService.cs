using DotNetCloud.Core.Modules.Supervisor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Core.Server.ModuleLoading;

/// <summary>
/// Discovered module information from the filesystem.
/// </summary>
/// <param name="ModuleId">The module identifier derived from the directory name.</param>
/// <param name="ExecutablePath">The full path to the module's executable.</param>
/// <param name="ManifestPath">The full path to the module's manifest.json, or null if not found.</param>
/// <param name="ModuleDirectory">The full path to the module's root directory.</param>
internal sealed record DiscoveredModule(
    string ModuleId,
    string ExecutablePath,
    string? ManifestPath,
    string ModuleDirectory);

/// <summary>
/// Discovers module binaries on the filesystem by scanning the configured modules directory.
/// </summary>
/// <remarks>
/// <para>
/// <b>Module Directory Convention:</b>
/// <code>
/// modules/
/// ├── dotnetcloud.files/
/// │   ├── dotnetcloud.files.dll        (or .exe)
/// │   ├── manifest.json                (optional, also supports code-based manifest)
/// │   └── appsettings.json             (optional module-specific config)
/// ├── dotnetcloud.chat/
/// │   ├── dotnetcloud.chat.dll
/// │   ├── manifest.json
/// │   └── appsettings.json
/// └── org.example.custom/
///     ├── org.example.custom.dll
///     └── manifest.json
/// </code>
/// </para>
/// <para>
/// Each module subdirectory name is used as the module identifier.
/// The module executable is expected to match the directory name with a .dll extension.
/// </para>
/// </remarks>
internal sealed class ModuleDiscoveryService
{
    private readonly ILogger<ModuleDiscoveryService> _logger;
    private readonly ProcessSupervisorOptions _options;

    public ModuleDiscoveryService(
        ILogger<ModuleDiscoveryService> logger,
        IOptions<ProcessSupervisorOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Scans the modules directory and returns all discovered modules.
    /// </summary>
    /// <returns>A list of discovered module information.</returns>
    public IReadOnlyList<DiscoveredModule> DiscoverModules()
    {
        var modulesDir = GetModulesDirectory();

        if (!Directory.Exists(modulesDir))
        {
            _logger.LogInformation("Modules directory not found at {Path}, creating it", modulesDir);
            Directory.CreateDirectory(modulesDir);
            return [];
        }

        var discovered = new List<DiscoveredModule>();

        foreach (var moduleDir in Directory.GetDirectories(modulesDir))
        {
            var module = TryDiscoverModule(moduleDir);
            if (module is not null)
            {
                discovered.Add(module);
                _logger.LogInformation(
                    "Discovered module {ModuleId} at {Path}",
                    module.ModuleId, module.ExecutablePath);
            }
        }

        _logger.LogInformation("Module discovery complete: {Count} modules found in {Path}",
            discovered.Count, modulesDir);

        return discovered;
    }

    /// <summary>
    /// Discovers a single module from a specific directory.
    /// </summary>
    /// <param name="moduleId">The expected module identifier.</param>
    /// <returns>The discovered module, or null if not found.</returns>
    public DiscoveredModule? DiscoverModule(string moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);

        var modulesDir = GetModulesDirectory();
        var moduleDir = Path.Combine(modulesDir, moduleId);

        if (!Directory.Exists(moduleDir))
        {
            _logger.LogWarning("Module directory not found for {ModuleId} at {Path}", moduleId, moduleDir);
            return null;
        }

        return TryDiscoverModule(moduleDir);
    }

    private DiscoveredModule? TryDiscoverModule(string moduleDir)
    {
        var dirName = Path.GetFileName(moduleDir);
        var moduleId = dirName;

        // Look for executable matching directory name
        var dllPath = Path.Combine(moduleDir, $"{dirName}.dll");
        var exePath = Path.Combine(moduleDir, $"{dirName}.exe");

        string? executablePath = null;
        if (File.Exists(dllPath))
        {
            executablePath = dllPath;
        }
        else if (File.Exists(exePath))
        {
            executablePath = exePath;
        }

        if (executablePath is null)
        {
            _logger.LogWarning(
                "No executable found for module {ModuleId} in {Path}. Expected {DllName} or {ExeName}",
                moduleId, moduleDir, $"{dirName}.dll", $"{dirName}.exe");
            return null;
        }

        // Look for manifest.json
        var manifestPath = Path.Combine(moduleDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            manifestPath = null;
        }

        return new DiscoveredModule(moduleId, executablePath, manifestPath, moduleDir);
    }

    private string GetModulesDirectory()
    {
        var path = _options.ModulesDirectory;

        // If relative, resolve against application base directory
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, path);
        }

        return path;
    }
}
