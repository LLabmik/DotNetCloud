using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.Notifications;

/// <summary>
/// Selects the appropriate <see cref="INotificationService"/> implementation
/// for the current operating system at runtime.
/// </summary>
public static class NotificationServiceFactory
{
    /// <summary>
    /// Creates the platform-appropriate <see cref="INotificationService"/>.
    /// <list type="bullet">
    ///   <item><description>Windows — Shell balloon tip via <c>Shell_NotifyIcon</c></description></item>
    ///   <item><description>Linux   — <c>notify-send</c> subprocess</description></item>
    ///   <item><description>Other   — no-op fallback</description></item>
    /// </list>
    /// </summary>
    public static INotificationService Create(ILogger<INotificationService> logger)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsNotificationService(logger);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxNotificationService(logger);

        logger.LogInformation(
            "No native notification service available on {OS}. Notifications will be silent.",
            RuntimeInformation.OSDescription);

        return new NoOpNotificationService();
    }
}
