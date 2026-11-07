using System.Text.Json;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Server.Supervisor;

namespace DotNetCloud.Core.Server.Sandbox;

/// <summary>
/// Per-module directories and the sanitized <c>module.json</c> handed to a sandboxed module.
/// </summary>
/// <remarks>
/// <para>
/// Layout: <c>&lt;dataRoot&gt;/modules/&lt;moduleId&gt;/{config,data,run}</c>. Directories are created
/// with owner-only permissions on Unix. On Windows the ACL follows the service account.
/// </para>
/// <para>
/// <c>module.json</c> deliberately contains only non-secret topology: module id, version,
/// data-store mode and the core/gRPC endpoints. It is never a copy of the core <c>config.json</c>
/// (which carries the core connection string, TLS material and the WOPI signing key).
/// </para>
/// </remarks>
internal sealed class ModuleSandboxLayout
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private ModuleSandboxLayout(string moduleId, string configDirectory, string dataDirectory, string runDirectory)
    {
        ModuleId = moduleId;
        ConfigDirectory = configDirectory;
        DataDirectory = dataDirectory;
        RunDirectory = runDirectory;
    }

    /// <summary>Gets the module id this layout belongs to.</summary>
    public string ModuleId { get; }

    /// <summary>Gets the directory holding <c>module.json</c>.</summary>
    public string ConfigDirectory { get; }

    /// <summary>Gets the module's private data directory.</summary>
    public string DataDirectory { get; }

    /// <summary>Gets the directory reserved for runtime artifacts (sockets in phase A4).</summary>
    public string RunDirectory { get; }

    /// <summary>Gets the path of the sanitized manifest copy.</summary>
    public string ModuleJsonPath => Path.Combine(ConfigDirectory, "module.json");

    /// <summary>
    /// Creates the layout for a module under <paramref name="dataRoot"/>.
    /// </summary>
    /// <param name="dataRoot">The server data directory.</param>
    /// <param name="moduleId">The module id (validated by the caller).</param>
    /// <returns>The layout.</returns>
    /// <exception cref="ArgumentException">The module id cannot form a safe directory name.</exception>
    public static ModuleSandboxLayout Create(string dataRoot, string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        foreach (var ch in moduleId)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('.' or '-' or '_'))
            {
                throw new ArgumentException($"Unsafe module id '{moduleId}'.", nameof(moduleId));
            }
        }

        var modulesRoot = Path.GetFullPath(Path.Combine(dataRoot, "modules"));
        var moduleRoot = Path.GetFullPath(Path.Combine(modulesRoot, moduleId));

        // Defence in depth: the resolved directory must stay under the modules root.
        if (!PathContainment.TryResolveWithin(modulesRoot, moduleRoot, out var resolvedModuleRoot))
        {
            throw new ArgumentException($"Module id '{moduleId}' escapes the modules data directory.", nameof(moduleId));
        }

        return new ModuleSandboxLayout(
            moduleId,
            Path.Combine(resolvedModuleRoot, "config"),
            Path.Combine(resolvedModuleRoot, "data"),
            Path.Combine(resolvedModuleRoot, "run"));
    }

    /// <summary>Creates the three directories with owner-only permissions.</summary>
    public void EnsureCreated()
    {
        foreach (var directory in new[] { ConfigDirectory, DataDirectory, RunDirectory })
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            TryRestrictToOwner(directory);
        }
    }

    /// <summary>
    /// Writes the sanitized <c>module.json</c>.
    /// </summary>
    /// <param name="version">The module version.</param>
    /// <param name="dataStoreMode">The declared data-store mode.</param>
    /// <param name="databaseProvider">The engine name (<c>PostgreSQL</c> / <c>SqlServer</c>).</param>
    /// <param name="databaseSchema">The provisioned module schema, when the module owns a data store.</param>
    /// <param name="coreEndpoint">The core gRPC endpoint the module may call.</param>
    /// <param name="grpcEndpoint">The module's own gRPC endpoint.</param>
    public void WriteModuleJson(
        string version,
        ModuleDataStoreMode dataStoreMode,
        string databaseProvider,
        string? databaseSchema,
        string coreEndpoint,
        string grpcEndpoint)
    {
        var document = new ModuleJsonDocument
        {
            ModuleId = ModuleId,
            Version = version,
            DataStore = dataStoreMode == ModuleDataStoreMode.Isolated ? "isolated" : "none",
            DatabaseProvider = databaseProvider,
            DatabaseSchema = databaseSchema ?? string.Empty,
            CoreEndpoint = coreEndpoint,
            GrpcEndpoint = grpcEndpoint,
        };

        File.WriteAllText(ModuleJsonPath, JsonSerializer.Serialize(document, JsonOptions));
        TryRestrictFileToOwner(ModuleJsonPath);
    }

    private static void TryRestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Best effort: a read-only or exotic filesystem must not stop the supervisor.
        }
    }

    private static void TryRestrictFileToOwner(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Best effort: a read-only or exotic filesystem must not stop the supervisor.
        }
    }

    private sealed record ModuleJsonDocument
    {
        public string ModuleId { get; init; } = string.Empty;

        public string Version { get; init; } = string.Empty;

        public string DataStore { get; init; } = "none";

        public string DatabaseProvider { get; init; } = string.Empty;

        public string DatabaseSchema { get; init; } = string.Empty;

        public string CoreEndpoint { get; init; } = string.Empty;

        public string GrpcEndpoint { get; init; } = string.Empty;
    }
}
