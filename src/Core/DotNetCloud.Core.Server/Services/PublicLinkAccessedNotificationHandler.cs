using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Sends a push notification to the share creator when their public link is accessed for the first time.
/// </summary>
internal sealed class PublicLinkAccessedNotificationHandler : IEventHandler<PublicLinkAccessedEvent>
{
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<PublicLinkAccessedNotificationHandler> _logger;

    public PublicLinkAccessedNotificationHandler(
        IPushNotificationService pushService,
        ILogger<PublicLinkAccessedNotificationHandler> logger)
    {
        _pushService = pushService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(PublicLinkAccessedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending public link accessed notification to user {UserId} for file {FileName}",
            @event.CreatedByUserId, @event.FileName);

        await _pushService.SendAsync(@event.CreatedByUserId, new PushNotification
        {
            Title = "Your public link was accessed",
            Body = $"Someone accessed your shared link for \"{@event.FileName}\".",
            Category = NotificationCategory.PublicLinkAccessed,
            Data = new Dictionary<string, string>
            {
                ["fileNodeId"] = @event.FileNodeId.ToString(),
                ["shareId"] = @event.ShareId.ToString()
            }
        }, cancellationToken);
    }
}
