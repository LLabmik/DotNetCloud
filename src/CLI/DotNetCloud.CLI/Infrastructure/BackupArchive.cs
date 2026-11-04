using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// One file that should be copied into a backup archive.
/// </summary>
/// <param name="SourcePath">Absolute path on disk.</param>
/// <param name="EntryName">Path inside the archive (always forward slashes).</param>
/// <param name="IsOptional">
/// When <see langword="true"/> a missing source is reported as a warning rather than failing the backup.
/// </param>
internal sealed record BackupPayloadEntry(string SourcePath, string EntryName, bool IsOptional = true);

/// <summary>
/// Metadata written to <c>backup-info.json</c> at the root of every backup archive. It records what the
/// archive contains so a restore can report (and adapt to) the environment the backup came from.
/// </summary>
internal sealed record BackupInfo
{
    /// <summary>Schema version of the backup layout. <c>2</c> adds system configuration + metadata.</summary>
    public int FormatVersion { get; init; } = BackupArchive.CurrentFormatVersion;

    /// <summary>Backup mode: <c>zip</c> (single or split archive) or <c>mirror</c> (incremental snapshot).</summary>
    public string Mode { get; init; } = "zip";

    /// <summary>Split size in bytes for zip sets (0 = single archive).</summary>
    public long SplitSizeBytes { get; init; }

    /// <summary>UTC creation timestamp (ISO-8601).</summary>
    public string CreatedUtc { get; init; } = string.Empty;

    /// <summary>Tool that produced the archive.</summary>
    public string Tool { get; init; } = "dotnetcloud backup";

    /// <summary>CLI informational version.</summary>
    public string CliVersion { get; init; } = string.Empty;

    /// <summary>Database provider (for example <c>PostgreSQL</c>).</summary>
    public string? DatabaseProvider { get; init; }

    /// <summary>Whether <c>database.sql</c> is present.</summary>
    public bool DatabaseDumpIncluded { get; init; }

    /// <summary>Whether <c>manifest/storage-manifest.tsv</c> is present.</summary>
    public bool StorageManifestIncluded { get; init; }

    /// <summary>Compression level used (<c>optimal</c>, <c>fastest</c>, <c>store</c>).</summary>
    public string Compression { get; init; } = "optimal";

    /// <summary>Original data directory (<c>DOTNETCLOUD_DATA_DIR</c>).</summary>
    public string? DataDirectory { get; init; }

    /// <summary>Original configuration directory.</summary>
    public string? ConfigDirectory { get; init; }

    /// <summary>Service account that must own the restored data directory.</summary>
    public string ServiceUser { get; init; } = "dotnetcloud";

    /// <summary>Non-data (system) paths copied into the archive, as archive entry names.</summary>
    public IReadOnlyList<string> SystemEntries { get; init; } = [];

    /// <summary>Number of data-directory files archived.</summary>
    public int DataFileCount { get; init; }

    /// <summary>Number of empty data-directory folders archived explicitly.</summary>
    public int EmptyDirectoryCount { get; init; }

    /// <summary>For mirror snapshots: the previous snapshot hardlinked from, if any.</summary>
    public string? PreviousSnapshot { get; init; }

    /// <summary>For mirror snapshots: files hardlinked from the previous snapshot.</summary>
    public int HardLinkedFiles { get; init; }

    /// <summary>For mirror snapshots: files copied from the source.</summary>
    public int CopiedFiles { get; init; }

    /// <summary>Total number of archive entries.</summary>
    public int TotalEntryCount { get; init; }

    /// <summary>Non-fatal problems encountered while creating the backup.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Helpers shared by the backup and restore commands: connection-string parsing, the system payload
/// definition (everything outside the data directory that a restore needs), safe entry resolution and
/// metadata serialization.
/// </summary>
internal static class BackupArchive
{
    /// <summary>Current backup layout version.</summary>
    internal const string InfoEntryName = "backup-info.json";

    /// <summary>Entry name of the plain-SQL database dump.</summary>
    internal const string DatabaseEntryName = "database.sql";

    /// <summary>Entry name of the embedded storage manifest.</summary>
    internal const string ManifestEntryName = "manifest/storage-manifest.tsv";

    /// <summary>Schema version of the current backup layout.</summary>
    internal const int CurrentFormatVersion = 2;

    /// <summary>Default data directory for a system install.</summary>
    internal const string DefaultDataDirectory = "/var/lib/dotnetcloud";

    /// <summary>Default configuration directory for a system install.</summary>
    internal const string DefaultConfigDirectory = "/etc/dotnetcloud";

    /// <summary>Default installation directory.</summary>
    internal const string DefaultInstallDirectory = "/opt/dotnetcloud";

    /// <summary>Default systemd unit directory.</summary>
    internal const string DefaultSystemdDirectory = "/etc/systemd/system";

    /// <summary>Default Collabora configuration directory.</summary>
    internal const string DefaultCoolwsdDirectory = "/etc/coolwsd";

    /// <summary>Default PATH wrapper for the CLI.</summary>
    internal const string DefaultCliWrapperPath = "/usr/local/bin/dotnetcloud";

    /// <summary>Service account name used on system installs.</summary>
    internal const string DefaultServiceUser = "dotnetcloud";

    /// <summary>
    /// Returns whether the current user can read a file. System configuration files are owned by root
    /// or by the <c>cool</c> group; a scheduled backup running as the service account must skip the
    /// ones it cannot open instead of failing the whole backup.
    /// </summary>
    internal static bool CanRead(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Platform default installation directory: <c>/opt/dotnetcloud</c> on Linux, the directory the
    /// CLI was launched from on Windows (or <c>DOTNETCLOUD_INSTALL_DIR</c>).
    /// </summary>
    internal static string GetDefaultInstallDirectory()
        => OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("DOTNETCLOUD_INSTALL_DIR")
               ?? AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))
            : DefaultInstallDirectory;

    private static readonly JsonSerializerOptions InfoJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Returns the informational version of the CLI assembly (for example <c>0.6.16</c>).
    /// </summary>
    internal static string GetCliVersion()
    {
        var assembly = typeof(BackupArchive).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    /// <summary>
    /// Reads a single key from a semicolon-delimited ADO.NET connection string. Matching is
    /// case-insensitive and trims surrounding whitespace around both key and value.
    /// </summary>
    /// <param name="connectionString">Connection string, or <see langword="null"/>.</param>
    /// <param name="keys">Accepted key aliases, in priority order.</param>
    /// <returns>The value when present and non-empty; otherwise <see langword="null"/>.</returns>
    internal static string? TryGetConnectionStringValue(string? connectionString, params string[] keys)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || keys.Length == 0)
            return null;

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eqIndex = part.IndexOf('=');
            if (eqIndex <= 0)
                continue;

            var key = part[..eqIndex].Trim();
            var value = part[(eqIndex + 1)..].Trim();

            if (keys.Any(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }

    /// <summary>
    /// Extracts the database password from a connection string.
    /// </summary>
    internal static string? TryGetConnectionStringPassword(string? connectionString)
        => TryGetConnectionStringValue(connectionString, "password", "pwd");

    /// <summary>
    /// Builds the environment variables a dump/restore tool needs in order to authenticate without an
    /// interactive prompt. PostgreSQL reads <c>PGPASSWORD</c>; SQL Server takes the password as a
    /// command-line argument instead.
    /// </summary>
    /// <param name="provider">Configured provider name.</param>
    /// <param name="connectionString">Connection string that may contain a password.</param>
    /// <returns>A dictionary of environment variables (possibly empty).</returns>
    internal static IReadOnlyDictionary<string, string> BuildToolEnvironment(
        string? provider, string? connectionString)
    {
        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (IsPostgreSql(provider))
        {
            var password = TryGetConnectionStringPassword(connectionString);
            if (!string.IsNullOrEmpty(password))
                environment["PGPASSWORD"] = password;
        }

        return environment;
    }

    /// <summary>Returns <see langword="true"/> for PostgreSQL provider aliases.</summary>
    internal static bool IsPostgreSql(string? provider)
        => provider is not null
           && (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)
               || provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)
               || provider.Equals("Npgsql", StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns <see langword="true"/> for SQL Server provider aliases.</summary>
    internal static bool IsSqlServer(string? provider)
        => provider is not null
           && (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
               || provider.Equals("MSSQL", StringComparison.OrdinalIgnoreCase)
               || provider.Equals("Microsoft.EntityFrameworkCore.SqlServer", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds the system payload: every file outside the data directory that a cold restore needs.
    /// </summary>
    /// <param name="configDirectory">Configuration directory (normally <c>/etc/dotnetcloud</c>).</param>
    /// <param name="installDirectory">Installation directory (normally <c>/opt/dotnetcloud</c>).</param>
    /// <param name="systemdDirectory">systemd unit directory.</param>
    /// <param name="coolwsdDirectory">Collabora configuration directory.</param>
    /// <param name="cliWrapperPath">CLI wrapper installed on <c>PATH</c>.</param>
    internal static IReadOnlyList<BackupPayloadEntry> GetSystemPayloadEntries(
        string? configDirectory = null,
        string? installDirectory = null,
        string? systemdDirectory = null,
        string? coolwsdDirectory = null,
        string? cliWrapperPath = null)
    {
        configDirectory ??= DefaultConfigDirectory;
        installDirectory ??= GetDefaultInstallDirectory();
        systemdDirectory ??= DefaultSystemdDirectory;
        coolwsdDirectory ??= DefaultCoolwsdDirectory;
        cliWrapperPath ??= DefaultCliWrapperPath;

        var entries = new List<BackupPayloadEntry>
        {
            // Configuration: DB connection string + Collabora/WOPI environment.
            new(Path.Combine(configDirectory, "config.json"), "config/config.json", IsOptional: false),
            new(Path.Combine(configDirectory, "env"), "config/env"),

            // TLS material: the PFX the server loads, the PEM bundle Collabora uses, and the root CA
            // that has to be installed into the system trust store on the restored machine.
            new(Path.Combine(configDirectory, "certs", "dotnetcloud-selfsigned.pfx"), "config/certs/dotnetcloud-selfsigned.pfx"),
            new(Path.Combine(configDirectory, "certs", "dotnetcloud-selfsigned.pem"), "config/certs/dotnetcloud-selfsigned.pem"),
            new(Path.Combine(configDirectory, "certs", "dotnetcloud-root-ca.crt"), "config/certs/dotnetcloud-root-ca.crt"),

            // Small, stateful-or-versioned files from the install directory. Binaries are deliberately
            // excluded: they are rebuilt by install.sh / scripts/deploy.sh.
            new(Path.Combine(installDirectory, "appsettings.json"), "opt/appsettings.json"),
            new(Path.Combine(installDirectory, "appsettings.Development.json"), "opt/appsettings.Development.json"),
            new(Path.Combine(installDirectory, "VERSION"), "opt/VERSION"),
        };

        if (OperatingSystem.IsLinux())
        {
            // Service definition. This is not always identical to the installer template: operators add
            // environment entries (for example Video__Enrichment__TmdbApiKey) that exist nowhere else.
            entries.Add(new(Path.Combine(systemdDirectory, "dotnetcloud.service"), "system/systemd/dotnetcloud.service"));
            entries.Add(new(cliWrapperPath, "system/bin/dotnetcloud"));

            // Collabora CODE configuration modified by the installer (WOPI alias group, TLS verification).
            entries.Add(new(Path.Combine(coolwsdDirectory, "coolwsd.xml"), "system/coolwsd/coolwsd.xml"));
            entries.Add(new(Path.Combine(coolwsdDirectory, "cert.pem"), "system/coolwsd/cert.pem"));
            entries.Add(new(Path.Combine(coolwsdDirectory, "ca-chain.cert.pem"), "system/coolwsd/ca-chain.cert.pem"));
        }

        // Enumerate extra certificate files and systemd drop-ins rather than hard-coding names, so a
        // future rotation (signing/encryption keys, ACME certificates) is captured automatically.
        var certsDirectory = Path.Combine(configDirectory, "certs");
        if (Directory.Exists(certsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(certsDirectory, "*", SearchOption.TopDirectoryOnly))
            {
                var entryName = "config/certs/" + Path.GetFileName(file);
                if (!entries.Any(e => e.EntryName.Equals(entryName, StringComparison.Ordinal)))
                    entries.Add(new BackupPayloadEntry(file, entryName));
            }
        }

        var dropInDirectory = Path.Combine(systemdDirectory, "dotnetcloud.service.d");
        if (Directory.Exists(dropInDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(dropInDirectory, "*", SearchOption.AllDirectories))
            {
                entries.Add(new BackupPayloadEntry(
                    file,
                    "system/systemd/dotnetcloud.service.d/" + Path.GetFileName(file)));
            }
        }

        return entries;
    }

    /// <summary>
    /// Returns the existing subset of <paramref name="entries"/> and records a warning for each
    /// optional entry that is missing.
    /// </summary>
    internal static IReadOnlyList<BackupPayloadEntry> ResolveExistingEntries(
        IEnumerable<BackupPayloadEntry> entries, ICollection<string> warnings)
    {
        var resolved = new List<BackupPayloadEntry>();
        foreach (var entry in entries)
        {
            if (File.Exists(entry.SourcePath))
            {
                resolved.Add(entry);
                continue;
            }

            if (entry.IsOptional)
                warnings.Add($"Optional backup source is missing: {entry.SourcePath}");
        }

        return resolved;
    }

    /// <summary>
    /// Validates an archive entry name and resolves it beneath <paramref name="rootDirectory"/>.
    /// Rejects absolute paths, drive-qualified paths and any <c>..</c> traversal (Zip Slip).
    /// </summary>
    /// <param name="rootDirectory">Destination root (for example the data directory).</param>
    /// <param name="entryName">Entry name inside the archive.</param>
    /// <param name="targetPath">Resolved absolute target when safe.</param>
    /// <param name="reason">Human-readable rejection reason when unsafe.</param>
    internal static bool TryResolveRestoreTarget(
        string rootDirectory, string entryName, out string targetPath, out string? reason)
    {
        targetPath = string.Empty;
        reason = null;

        if (string.IsNullOrWhiteSpace(entryName))
        {
            reason = "empty entry name";
            return false;
        }

        var normalized = entryName.Replace('\\', '/');

        // Check rootedness before trimming: an entry named "/etc/passwd" must be rejected, not
        // silently turned into "etc/passwd" under the destination root.
        if (Path.IsPathRooted(normalized)
            || (normalized.Length >= 2 && normalized[1] == ':'))
        {
            reason = "absolute path";
            return false;
        }

        normalized = normalized.TrimStart('/');
        if (normalized.Length == 0)
        {
            reason = "empty entry name";
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s == ".."))
        {
            reason = "path traversal";
            return false;
        }

        var fullRoot = Path.GetFullPath(rootDirectory);
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!candidate.Equals(fullRoot, comparison)
            && !candidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, comparison))
        {
            reason = "outside the destination directory";
            return false;
        }

        targetPath = candidate;
        return true;
    }

    /// <summary>Serializes backup metadata for <c>backup-info.json</c>.</summary>
    internal static string SerializeInfo(BackupInfo info)
        => JsonSerializer.Serialize(info, InfoJsonOptions);

    /// <summary>Deserializes <c>backup-info.json</c>, returning <see langword="null"/> on bad input.</summary>
    internal static BackupInfo? DeserializeInfo(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<BackupInfo>(json, InfoJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
