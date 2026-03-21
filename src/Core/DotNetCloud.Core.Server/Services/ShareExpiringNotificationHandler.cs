using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Sends a push notification to the share creator when their share is about to expire.
/// </summary>
internal sealed class ShareExpiringNotificationHandler : IEventHandler<ShareExpiringEvent>
{
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<ShareExpiringNotificationHandler> _logger;

    public ShareExpiringNotificationHandler(
        IPushNotificationService pushService,
        ILogger<ShareExpiringNotificationHandler> logger)
    {
        _pushService = pushService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ShareExpiringEvent @event, CancellationToken cancellationToken = default)
    {
        var timeLeft = @event.ExpiresAt - DateTime.UtcNow;
        var hoursLeft = Math.Max(1, (int)Math.Ceiling(timeLeft.TotalHours));

        _logger.LogInformation("Sending share expiry notification to user {UserId} for file {FileName}, expires in {Hours}h",
            @event.CreatedByUserId, @event.FileName, hoursLeft);

        await _pushService.SendAsync(@event.CreatedByUserId, new PushNotification
        {
            Title = "Share expiring soon",
            Body = $"Your share for \"{@event.FileName}\" expires in ~{hoursLeft} hour{(hoursLeft == 1 ? "" : "s")}.",
            Category = NotificationCategory.ShareExpiring,
            Data = new Dictionary<string, string>
            {
                ["fileNodeId"] = @event.FileNodeId.ToString(),
                ["shareId"] = @event.ShareId.ToString(),
                ["expiresAt"] = @event.ExpiresAt.ToString("O")
            }
        }, cancellationToken);
    }
}
