using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Sends push notifications when a resource (note, contact, calendar) is shared with a user.
/// </summary>
internal sealed class ResourceSharedNotificationHandler : IEventHandler<ResourceSharedEvent>
{
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<ResourceSharedNotificationHandler> _logger;

    public ResourceSharedNotificationHandler(
        IPushNotificationService pushService,
        ILogger<ResourceSharedNotificationHandler> logger)
    {
        _pushService = pushService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ResourceSharedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending share notification to user {UserId} for {EntityType} \"{EntityName}\" from module {Module}",
            @event.SharedWithUserId, @event.EntityType, @event.EntityDisplayName, @event.SourceModuleId);

        await _pushService.SendAsync(@event.SharedWithUserId, new PushNotification
        {
            Title = $"{@event.EntityType} shared with you",
            Body = $"\"{@event.EntityDisplayName}\" has been shared with you ({@event.Permission}).",
            Category = NotificationCategory.ResourceShared,
            Data = new Dictionary<string, string>
            {
                ["entityType"] = @event.EntityType,
                ["entityId"] = @event.EntityId.ToString(),
                ["sourceModule"] = @event.SourceModuleId,
                ["permission"] = @event.Permission
            }
        }, cancellationToken);
    }
}
