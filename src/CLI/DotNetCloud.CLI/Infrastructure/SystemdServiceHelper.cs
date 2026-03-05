using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Writes and manages the systemd service unit file for DotNetCloud.
/// Supports two modes: a permissive unit for initial installation and a
/// hardened unit that is applied after setup completes successfully.
/// </summary>
internal static class SystemdServiceHelper
{
    private const string ServiceName = "dotnetcloud.service";
    private const string ServicePath = "/etc/systemd/system/" + ServiceName;
    private const string InstallDir = "/opt/dotnetcloud";
    private const string DataDir = "/var/lib/dotnetcloud";
    private const string LogDir = "/var/log/dotnetcloud";
    private const string RunDir = "/run/dotnetcloud";
    private const string ConfigDir = "/etc/dotnetcloud";
    private const string ServiceUser = "dotnetcloud";
    private const string ServiceGroup = "dotnetcloud";

    /// <summary>
    /// Returns <c>true</c> when running on Linux and the systemd service file exists.
    /// </summary>
    public static bool ServiceFileExists()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists(ServicePath);
    }

    /// <summary>
    /// Applies security hardening directives to the systemd service unit file
    /// and reloads systemd. Call this after setup completes successfully.
    /// </summary>
    /// <returns><c>true</c> if the service file was updated; <c>false</c> if not applicable.</returns>
    public static bool ApplyHardening()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return false;
        }

        if (!File.Exists(ServicePath))
        {
            return false;
        }

        var contents = File.ReadAllText(ServicePath);

        // Already hardened — nothing to do.
        if (contents.Contains("NoNewPrivileges=true", StringComparison.Ordinal))
        {
            return false;
        }

        var hardenedUnit = GenerateUnitFile(hardened: true);
        File.WriteAllText(ServicePath, hardenedUnit);

        // Reload systemd so it picks up the updated unit.
        RunSystemctl("daemon-reload");

        return true;
    }

    /// <summary>
    /// Generates the systemd unit file content.
    /// </summary>
    /// <param name="hardened">
    /// When <c>true</c>, includes <c>NoNewPrivileges</c>, <c>ProtectSystem</c>,
    /// <c>ProtectHome</c>, and <c>PrivateTmp</c> directives.
    /// </param>
    internal static string GenerateUnitFile(bool hardened)
    {
        var hardeningBlock = hardened
            ? $"""

            # Security hardening (applied by dotnetcloud setup)
            NoNewPrivileges=true
            ProtectSystem=strict
            ProtectHome=true
            ReadWritePaths={DataDir} {LogDir} {RunDir} {ConfigDir}
            PrivateTmp=true
            """
            : "";

        return $"""
            [Unit]
            Description=DotNetCloud Core Server
            Documentation=https://github.com/LLabmik/DotNetCloud
            After=network.target postgresql.service
            Requires=network.target

            [Service]
            Type=notify
            User={ServiceUser}
            Group={ServiceGroup}
            WorkingDirectory={InstallDir}
            ExecStart={InstallDir}/dotnetcloud start
            ExecStop={InstallDir}/dotnetcloud stop
            Restart=on-failure
            RestartSec=10
            TimeoutStartSec=60
            TimeoutStopSec=30
            {hardeningBlock}
            # Environment
            Environment=DOTNET_ENVIRONMENT=Production
            Environment=DOTNETCLOUD_CONFIG_DIR={ConfigDir}
            Environment=DOTNETCLOUD_DATA_DIR={DataDir}
            Environment=DOTNETCLOUD_LOG_DIR={LogDir}

            [Install]
            WantedBy=multi-user.target
            """.Replace("            ", ""); // Strip leading indentation from raw string literal
    }

    /// <summary>
    /// Enables the service on boot and starts it immediately.
    /// </summary>
    /// <returns><c>true</c> if systemctl exited successfully; <c>false</c> otherwise.</returns>
    public static bool EnableAndStart()
    {
        if (!ServiceFileExists())
        {
            return false;
        }

        return RunSystemctl("enable --now dotnetcloud.service");
    }

    private static bool RunSystemctl(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("systemctl", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            // Best-effort — systemctl may not be available in containers or during tests.
            return false;
        }
    }
}
