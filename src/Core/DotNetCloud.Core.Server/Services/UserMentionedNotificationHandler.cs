using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Sends push notifications when a user is mentioned in content.
/// </summary>
internal sealed class UserMentionedNotificationHandler : IEventHandler<UserMentionedEvent>
{
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<UserMentionedNotificationHandler> _logger;

    public UserMentionedNotificationHandler(
        IPushNotificationService pushService,
        ILogger<UserMentionedNotificationHandler> logger)
    {
        _pushService = pushService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(UserMentionedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending mention notification to user {UserId} in {ContentType} \"{ContentTitle}\"",
            @event.MentionedUserId, @event.ContentType, @event.ContentTitle);

        await _pushService.SendAsync(@event.MentionedUserId, new PushNotification
        {
            Title = $"You were mentioned in a {@event.ContentType.ToLowerInvariant()}",
            Body = $"In \"{@event.ContentTitle}\"",
            Category = NotificationCategory.Mention,
            Data = new Dictionary<string, string>
            {
                ["contentType"] = @event.ContentType,
                ["contentId"] = @event.ContentId.ToString(),
                ["sourceModule"] = @event.SourceModuleId
            }
        }, cancellationToken);
    }
}
