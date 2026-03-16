using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Sends push notifications when a user's storage quota reaches warning or critical thresholds.
/// </summary>
internal sealed class QuotaNotificationHandler :
    IEventHandler<QuotaWarningEvent>,
    IEventHandler<QuotaCriticalEvent>
{
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<QuotaNotificationHandler> _logger;

    public QuotaNotificationHandler(
        IPushNotificationService pushService,
        ILogger<QuotaNotificationHandler> logger)
    {
        _pushService = pushService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(QuotaWarningEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending quota warning notification to user {UserId} ({UsagePercent:F0}%)",
            @event.UserId, @event.UsagePercent);

        await _pushService.SendAsync(@event.UserId, new PushNotification
        {
            Title = "Storage almost full",
            Body = $"You're using {FormatBytes(@event.UsedBytes)} of {FormatBytes(@event.MaxBytes)} ({@event.UsagePercent:F0}%).",
            Category = NotificationCategory.QuotaWarning,
            Data = new Dictionary<string, string>
            {
                ["usedBytes"] = @event.UsedBytes.ToString(),
                ["maxBytes"] = @event.MaxBytes.ToString(),
                ["usagePercent"] = @event.UsagePercent.ToString("F1")
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(QuotaCriticalEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending quota critical notification to user {UserId} ({UsagePercent:F0}%)",
            @event.UserId, @event.UsagePercent);

        await _pushService.SendAsync(@event.UserId, new PushNotification
        {
            Title = "Storage nearly full",
            Body = $"You're using {FormatBytes(@event.UsedBytes)} of {FormatBytes(@event.MaxBytes)} ({@event.UsagePercent:F0}%). Free up space to continue uploading.",
            Category = NotificationCategory.QuotaCritical,
            Data = new Dictionary<string, string>
            {
                ["usedBytes"] = @event.UsedBytes.ToString(),
                ["maxBytes"] = @event.MaxBytes.ToString(),
                ["usagePercent"] = @event.UsagePercent.ToString("F1")
            }
        }, cancellationToken);
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{bytes / 1_024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }
}
