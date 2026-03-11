using System.Diagnostics;
using System.Text;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.Notifications;

/// <summary>
/// Windows notification service that delivers Windows toast notifications.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsNotificationService : INotificationService
{
    private readonly ILogger<INotificationService> _logger;

    /// <inheritdoc/>
    public Action<string>? OnNotificationActivated { get; set; }

    /// <summary>Initializes the service and registers toast activation handling.</summary>
    public WindowsNotificationService(ILogger<INotificationService> logger)
    {
        _logger = logger;
    }

    // ── INotificationService ──────────────────────────────────────────────

    /// <inheritdoc/>
    public void ShowNotification(
        string title,
        string body,
        NotificationType type = NotificationType.Info,
        string? actionUrl = null,
        string? groupKey = null,
        string? replaceKey = null)
    {
        try
        {
            var safeTitle = string.IsNullOrWhiteSpace(title) ? "DotNetCloud" : title;
            var safeBody = string.IsNullOrWhiteSpace(body) ? " " : body;
            var toastXml = BuildToastXml(safeTitle, safeBody, actionUrl, type);
            var script = BuildToastScript(toastXml, groupKey, replaceKey);
            var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

            using var process = Process.Start(new ProcessStartInfo("powershell.exe")
            {
                ArgumentList =
                {
                    "-NoProfile",
                    "-NonInteractive",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-EncodedCommand",
                    encodedScript,
                },
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to display Windows toast notification.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string BuildToastXml(string title, string body, string? actionUrl, NotificationType type)
    {
        var escapedTitle = EscapeXml(title);
        var escapedBody = EscapeXml(body);
        var attribution = type is NotificationType.Mention or NotificationType.Warning or NotificationType.Error
            ? $"<text placement='attribution'>{EscapeXml(type.ToString())}</text>"
            : string.Empty;

        var actionBlock = string.IsNullOrWhiteSpace(actionUrl)
            ? string.Empty
            : $"<actions><action content='Open Chat' arguments='{EscapeXml(actionUrl)}' activationType='protocol'/></actions>";

        return $"""
<toast>
  <visual>
    <binding template='ToastGeneric'>
      <text>{escapedTitle}</text>
      <text>{escapedBody}</text>
            {attribution}
    </binding>
  </visual>
  {actionBlock}
  <audio silent='true'/>
</toast>
""";
    }

    private static string BuildToastScript(string toastXml, string? groupKey, string? replaceKey)
    {
        var escapedXml = toastXml.Replace("@", "@@", StringComparison.Ordinal);
        var safeGroupKey = EscapePowerShellSingleQuoted(groupKey);
        var safeReplaceKey = EscapePowerShellSingleQuoted(replaceKey);

        var script = new StringBuilder();
        script.AppendLine("[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] > $null");
        script.AppendLine("[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] > $null");
        script.AppendLine();
        script.AppendLine("$toastXml = @\"");
        script.AppendLine(escapedXml);
        script.AppendLine("\"@");
        script.AppendLine();
        script.AppendLine("$xml = New-Object Windows.Data.Xml.Dom.XmlDocument");
        script.AppendLine("$xml.LoadXml($toastXml)");
        script.AppendLine();
        script.AppendLine("$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)");
        script.AppendLine("$toast.ExpirationTime = [DateTimeOffset]::Now.AddSeconds(5)");
        script.AppendLine();
        script.AppendLine($"$groupKey = '{safeGroupKey}'");
        script.AppendLine("if (-not [string]::IsNullOrWhiteSpace($groupKey)) {");
        script.AppendLine("    $toast.Group = $groupKey");
        script.AppendLine("}");
        script.AppendLine();
        script.AppendLine($"$replaceKey = '{safeReplaceKey}'");
        script.AppendLine("if (-not [string]::IsNullOrWhiteSpace($replaceKey)) {");
        script.AppendLine("    $toast.Tag = $replaceKey");
        script.AppendLine("}");
        script.AppendLine();
        script.AppendLine("$notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('DotNetCloud.SyncTray')");
        script.AppendLine("$notifier.Show($toast)");

        return script.ToString();
    }

    private static string EscapePowerShellSingleQuoted(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
