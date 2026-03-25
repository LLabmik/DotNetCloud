using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.Startup;

public interface IDesktopStartupManager
{
    bool TryApplyStartOnLogin(bool enable);

    bool TryEnsureApplicationLauncher();
}

internal sealed class DesktopStartupManager : IDesktopStartupManager
{
    private const string LinuxDesktopFileName = "dotnetcloud-sync-tray.desktop";

    private readonly ILogger<DesktopStartupManager> _logger;
    private readonly Func<string?> _trayExecutablePathProvider;
    private readonly Func<string?> _launcherIconPathProvider;
    private readonly string _autostartDirectory;
    private readonly string _applicationsDirectory;
    private readonly Func<bool> _isLinux;

    public DesktopStartupManager(
        ILogger<DesktopStartupManager> logger,
        Func<string?>? trayExecutablePathProvider = null,
        Func<string?>? launcherIconPathProvider = null,
        string? autostartDirectory = null,
        string? applicationsDirectory = null,
        Func<bool>? isLinux = null)
    {
        _logger = logger;
        _trayExecutablePathProvider = trayExecutablePathProvider ?? ResolveTrayExecutablePath;
        _launcherIconPathProvider = launcherIconPathProvider ?? ResolveLauncherIconPath;
        _autostartDirectory = autostartDirectory ?? ResolveAutostartDirectory();
        _applicationsDirectory = applicationsDirectory ?? ResolveApplicationsDirectory();
        _isLinux = isLinux ?? OperatingSystem.IsLinux;
    }

    public bool TryApplyStartOnLogin(bool enable)
    {
        if (!_isLinux())
        {
            _logger.LogDebug("Start-on-login registration is currently only implemented for Linux desktop sessions.");
            return true;
        }

        var desktopFilePath = GetAutostartDesktopFilePath();

        try
        {
            if (!enable)
            {
                if (File.Exists(desktopFilePath))
                {
                    File.Delete(desktopFilePath);
                }

                _logger.LogInformation("Removed SyncTray autostart entry at {Path}.", desktopFilePath);
                return true;
            }

            var trayExecutablePath = _trayExecutablePathProvider();
            if (string.IsNullOrWhiteSpace(trayExecutablePath) || !File.Exists(trayExecutablePath))
            {
                _logger.LogWarning("Cannot enable start-on-login because the SyncTray executable was not found.");
                return false;
            }

            Directory.CreateDirectory(_autostartDirectory);
            File.WriteAllText(desktopFilePath, BuildDesktopEntry(trayExecutablePath));
            _logger.LogInformation("Created SyncTray autostart entry at {Path}.", desktopFilePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update SyncTray autostart entry at {Path}.", desktopFilePath);
            return false;
        }
    }

    public bool TryEnsureApplicationLauncher()
    {
        if (!_isLinux())
        {
            return true;
        }

        var trayExecutablePath = _trayExecutablePathProvider();
        if (string.IsNullOrWhiteSpace(trayExecutablePath) || !File.Exists(trayExecutablePath))
        {
            _logger.LogWarning("Cannot create Linux application launcher because the SyncTray executable was not found.");
            return false;
        }

        var launcherPath = GetApplicationsDesktopFilePath();

        try
        {
            Directory.CreateDirectory(_applicationsDirectory);
            var iconPath = _launcherIconPathProvider();
            File.WriteAllText(launcherPath, BuildLauncherDesktopEntry(trayExecutablePath, iconPath));
            _logger.LogInformation("Ensured Linux application launcher at {Path}.", launcherPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Linux application launcher at {Path}.", launcherPath);
            return false;
        }
    }

    internal string GetAutostartDesktopFilePath() => Path.Combine(_autostartDirectory, LinuxDesktopFileName);

    internal string GetApplicationsDesktopFilePath() => Path.Combine(_applicationsDirectory, LinuxDesktopFileName);

    internal static string BuildDesktopEntry(string trayExecutablePath)
    {
        var escapedExec = EscapeDesktopExecValue(trayExecutablePath);

        return string.Join(
            Environment.NewLine,
            [
                "[Desktop Entry]",
                "Type=Application",
                "Version=1.0",
                "Name=DotNetCloud Sync",
                "Comment=Start DotNetCloud SyncTray and background sync service",
                $"Exec={escapedExec}",
                "Terminal=false",
                "StartupNotify=false",
                "X-GNOME-Autostart-enabled=true",
                "Categories=Network;Utility;",
                string.Empty,
            ]);
    }

    internal static string BuildLauncherDesktopEntry(string trayExecutablePath, string? iconPath)
    {
        var escapedExec = EscapeDesktopExecValue(trayExecutablePath);
        var iconLine = string.IsNullOrWhiteSpace(iconPath)
            ? "Icon=cloud"
            : $"Icon={iconPath}";

        return string.Join(
            Environment.NewLine,
            [
                "[Desktop Entry]",
                "Type=Application",
                "Version=1.0",
                "Name=DotNetCloud Sync Client",
                "Comment=Open DotNetCloud SyncTray",
                $"Exec={escapedExec}",
                iconLine,
                "Terminal=false",
                "StartupNotify=true",
                "Categories=Network;Utility;",
                "Keywords=DotNetCloud;Sync;Client;",
                string.Empty,
            ]);
    }

    internal static string? ResolveLauncherIconPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "dotnetcloud-sync-cloud.svg"),
            Path.Combine(AppContext.BaseDirectory, "dotnetcloud-sync-cloud.svg"),
        };

        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
    }

    internal static string? ResolveTrayExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            return processPath;
        }

        var executableName = OperatingSystem.IsWindows() ? "dotnetcloud-sync-tray.exe" : "dotnetcloud-sync-tray";
        var candidate = Path.Combine(AppContext.BaseDirectory, executableName);
        return File.Exists(candidate) ? candidate : null;
    }

    internal static string? ResolveServiceExecutablePath()
    {
        var executableName = OperatingSystem.IsWindows() ? "dotnetcloud-sync-service.exe" : "dotnetcloud-sync-service";
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, executableName),
            Path.Combine(AppContext.BaseDirectory, "SyncService", executableName),
            Path.Combine(AppContext.BaseDirectory, "..", "SyncService", executableName),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "opt",
                "dotnetcloud-desktop-client",
                "SyncService",
                executableName),
        };

        return candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(File.Exists);
    }

    private static string ResolveAutostartDirectory()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(configHome))
        {
            return Path.Combine(configHome, "autostart");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "autostart");
    }

    private static string ResolveApplicationsDirectory()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(dataHome))
        {
            return Path.Combine(dataHome, "applications");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local",
            "share",
            "applications");
    }

    private static string EscapeDesktopExecValue(string executablePath)
    {
        var escaped = executablePath
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        return $"\"{escaped}\"";
    }
}