using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.Notifications;

/// <summary>
/// Linux notification service that delegates to <c>notify-send</c> (libnotify).
/// Requires the <c>libnotify-bin</c> package to be installed on the host system.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxNotificationService : INotificationService
{
    private readonly ILogger<INotificationService> _logger;

    /// <summary>Initializes a new <see cref="LinuxNotificationService"/>.</summary>
    public LinuxNotificationService(ILogger<INotificationService> logger) => _logger = logger;

    /// <inheritdoc/>
    public void ShowNotification(string title, string body, NotificationType type = NotificationType.Info)
    {
        var urgency = type switch
        {
            NotificationType.Warning => "normal",
            NotificationType.Error => "critical",
            _ => "low",
        };

        // Sanitise inputs to avoid shell injection via notify-send argument parsing.
        var safeTitle = Sanitise(title);
        var safeBody = Sanitise(body);

        try
        {
            var psi = new ProcessStartInfo("notify-send")
            {
                ArgumentList =
                {
                    "--app-name=DotNetCloud Sync",
                    $"--urgency={urgency}",
                    "--expire-time=5000",
                    "--icon=folder-sync",
                    safeTitle,
                    safeBody,
                },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(2000);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not send notification via notify-send. Is libnotify-bin installed?");
        }
    }

    // Prevent shell injection by stripping newlines and null chars.
    private static string Sanitise(string value) =>
        value.Replace('\n', ' ').Replace('\r', ' ').Replace('\0', ' ');
}
