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

    /// <inheritdoc/>
    public Action<string>? OnNotificationActivated { get; set; }

    /// <summary>Initializes a new <see cref="LinuxNotificationService"/>.</summary>
    public LinuxNotificationService(ILogger<INotificationService> logger) => _logger = logger;

    /// <inheritdoc/>
    public void ShowNotification(string title, string body, NotificationType type = NotificationType.Info, string? actionUrl = null)
    {
        var urgency = type switch
        {
            NotificationType.Chat => "normal",
            NotificationType.Mention => "critical",
            NotificationType.Warning => "normal",
            NotificationType.Error => "critical",
            _ => "low",
        };

        var icon = type switch
        {
            NotificationType.Chat => "mail-unread",
            NotificationType.Mention => "mail-mark-important",
            NotificationType.Warning => "dialog-warning",
            NotificationType.Error => "dialog-error",
            _ => "folder-sync",
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
                    $"--icon={icon}",
                    "--wait",
                    safeTitle,
                    safeBody,
                },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            if (!string.IsNullOrWhiteSpace(actionUrl))
                psi.ArgumentList.Add("--action=open-chat=Open Chat");

            using var process = Process.Start(psi);
            if (process is null)
                return;

            process.WaitForExit(6000);

            if (!string.IsNullOrWhiteSpace(actionUrl))
            {
                var output = process.StandardOutput.ReadToEnd();
                if (output.Contains("open-chat", StringComparison.OrdinalIgnoreCase))
                {
                    TryOpenUrl(actionUrl);
                    OnNotificationActivated?.Invoke(actionUrl);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not send notification via notify-send. Is libnotify-bin installed?");
        }
    }

    private void TryOpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo("xdg-open", url)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to launch xdg-open for notification URL.");
        }
    }

    // Prevent shell injection by stripping newlines and null chars.
    private static string Sanitise(string value) =>
        value.Replace('\n', ' ').Replace('\r', ' ').Replace('\0', ' ');
}
