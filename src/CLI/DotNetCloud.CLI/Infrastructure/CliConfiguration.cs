using System.Text.Json;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Manages CLI configuration file loading and persistence.
/// Configuration is resolved in priority order:
/// 1. DOTNETCLOUD_CONFIG_DIR environment variable (set by systemd unit)
/// 2. /etc/dotnetcloud (system install on Linux)
/// 3. Platform-specific user config (~/.config/dotnetcloud on Linux, %APPDATA%\dotnetcloud on Windows)
/// </summary>
internal static class CliConfiguration
{
    private const string ConfigFileName = "config.json";
    private const string SystemConfigDir = "/etc/dotnetcloud";

    private static readonly string ConfigDirectory = ResolveConfigDirectory();
    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, ConfigFileName);

    /// <summary>
    /// Whether the resolved config directory is the system install path (/etc/dotnetcloud).
    /// Used to set FHS-compliant defaults for data/log/backup directories.
    /// </summary>
    internal static bool IsSystemInstall => ConfigDirectory == SystemConfigDir;

    private static string ResolveConfigDirectory()
    {
        // 1. Explicit env var (set by systemd unit, Docker, etc.)
        var envDir = Environment.GetEnvironmentVariable("DOTNETCLOUD_CONFIG_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
        {
            return envDir;
        }

        // 2. System install path on Linux (created by install.sh)
        if (!OperatingSystem.IsWindows() && Directory.Exists(SystemConfigDir))
        {
            return SystemConfigDir;
        }

        // 3. User-local config (dev/Windows)
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "dotnetcloud");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Loads the CLI configuration from disk.
    /// Returns a default configuration if the file does not exist.
    /// </summary>
    public static CliConfig Load()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return new CliConfig();
        }

        var json = File.ReadAllText(ConfigFilePath);
        return JsonSerializer.Deserialize<CliConfig>(json, JsonOptions) ?? new CliConfig();
    }

    /// <summary>
    /// Attempts to load the CLI configuration without throwing.
    /// </summary>
    /// <param name="config">Loaded configuration when successful; otherwise a default instance.</param>
    /// <param name="errorMessage">A user-facing error message when loading fails.</param>
    /// <returns><c>true</c> when the config file was read and parsed successfully; otherwise <c>false</c>.</returns>
    public static bool TryLoad(out CliConfig config, out string? errorMessage)
    {
        config = new CliConfig();
        errorMessage = null;

        if (!File.Exists(ConfigFilePath))
        {
            errorMessage = $"Configuration file not found: {ConfigFilePath}";
            return false;
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            config = JsonSerializer.Deserialize<CliConfig>(json, JsonOptions) ?? new CliConfig();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            errorMessage = $"Permission denied reading '{ConfigFilePath}'. " +
                "Run with sudo or add your user to the 'dotnetcloud' group.";
            return false;
        }
        catch (IOException ex)
        {
            errorMessage = $"Could not read configuration file '{ConfigFilePath}': {ex.Message}";
            return false;
        }
        catch (JsonException ex)
        {
            errorMessage = $"Configuration file '{ConfigFilePath}' is invalid JSON: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Saves the CLI configuration to disk.
    /// </summary>
    public static void Save(CliConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }

    /// <summary>
    /// Returns the path to the configuration file.
    /// </summary>
    public static string GetConfigFilePath() => ConfigFilePath;

    /// <summary>
    /// Returns the resolved configuration directory path.
    /// </summary>
    public static string GetConfigDirectory() => ConfigDirectory;

    /// <summary>
    /// Checks whether a configuration file exists.
    /// </summary>
    public static bool ConfigExists() => File.Exists(ConfigFilePath);
}

/// <summary>
/// Represents the persisted CLI configuration (database, server, modules).
/// </summary>
internal sealed class CliConfig
{
    /// <summary>
    /// The database provider name (PostgreSQL, SqlServer, MariaDB).
    /// </summary>
    public string DatabaseProvider { get; set; } = string.Empty;

    /// <summary>
    /// The database connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// The server HTTP port.
    /// </summary>
    public int HttpPort { get; set; } = 5080;

    /// <summary>
    /// The server HTTPS port.
    /// </summary>
    public int HttpsPort { get; set; } = 5443;

    /// <summary>
    /// Whether HTTPS is enabled.
    /// </summary>
    public bool EnableHttps { get; set; } = true;

    /// <summary>
    /// The path to the TLS certificate file (PFX or PEM).
    /// </summary>
    public string? TlsCertificatePath { get; set; }

    /// <summary>
    /// Whether Let's Encrypt automatic certificate provisioning is enabled.
    /// </summary>
    public bool UseLetsEncrypt { get; set; }

    /// <summary>
    /// Whether a private self-signed TLS certificate should be generated and used.
    /// </summary>
    public bool UseSelfSignedTls { get; set; }

    /// <summary>
    /// The domain name for Let's Encrypt (e.g., "cloud.example.com").
    /// </summary>
    public string? LetsEncryptDomain { get; set; }

    /// <summary>
    /// Hostname or IP used as the primary subject/SAN when generating a self-signed TLS certificate.
    /// </summary>
    public string? SelfSignedTlsHost { get; set; }

    /// <summary>
    /// The organization name created during setup.
    /// </summary>
    public string? OrganizationName { get; set; }

    /// <summary>
    /// The admin user email created during setup.
    /// </summary>
    public string? AdminEmail { get; set; }

    /// <summary>
    /// List of module IDs selected during setup.
    /// </summary>
    public List<string> EnabledModules { get; set; } = [];

    /// <summary>
    /// The path to the data directory for file storage.
    /// Defaults to FHS path on system installs, user-local path otherwise.
    /// </summary>
    public string DataDirectory { get; set; } = CliConfiguration.IsSystemInstall
        ? "/var/lib/dotnetcloud"
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "dotnetcloud", "data");

    /// <summary>
    /// The path to the log directory.
    /// Defaults to FHS path on system installs, user-local path otherwise.
    /// </summary>
    public string LogDirectory { get; set; } = CliConfiguration.IsSystemInstall
        ? "/var/log/dotnetcloud"
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "dotnetcloud", "logs");

    /// <summary>
    /// The backup output directory.
    /// Defaults to FHS path on system installs, user-local path otherwise.
    /// </summary>
    public string BackupDirectory { get; set; } = CliConfiguration.IsSystemInstall
        ? "/var/lib/dotnetcloud/backups"
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "dotnetcloud", "backups");

    /// <summary>
    /// How Collabora Online is configured: None, BuiltIn, or External.
    /// </summary>
    public string CollaboraMode { get; set; } = "None";

    /// <summary>
    /// URL of an externally-managed Collabora Online server.
    /// Used when <see cref="CollaboraMode"/> is "External".
    /// </summary>
    public string CollaboraUrl { get; set; } = string.Empty;

    /// <summary>
    /// Directory where the built-in Collabora CODE is installed.
    /// Used when <see cref="CollaboraMode"/> is "BuiltIn".
    /// On system installs Collabora is managed via APT, so this is not used.
    /// </summary>
    public string CollaboraDirectory { get; set; } = CliConfiguration.IsSystemInstall
        ? "/usr/share/coolwsd"
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "dotnetcloud", "collabora");

    /// <summary>
    /// When the setup wizard was last run.
    /// </summary>
    public DateTime? SetupCompletedAt { get; set; }
}
