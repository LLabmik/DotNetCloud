using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Sends push notifications when a reminder fires.
/// </summary>
internal sealed class ReminderNotificationHandler : IEventHandler<ReminderTriggeredEvent>
{
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<ReminderNotificationHandler> _logger;

    public ReminderNotificationHandler(
        IPushNotificationService pushService,
        ILogger<ReminderNotificationHandler> logger)
    {
        _pushService = pushService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ReminderTriggeredEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending reminder notification to user {UserId} for {EntityType} \"{Title}\"",
            @event.UserId, @event.EntityType, @event.Title);

        var data = new Dictionary<string, string>
        {
            ["entityType"] = @event.EntityType,
            ["entityId"] = @event.EntityId.ToString(),
            ["sourceModule"] = @event.SourceModuleId
        };

        if (@event.DueAtUtc.HasValue)
        {
            data["dueAtUtc"] = @event.DueAtUtc.Value.ToString("O");
        }

        await _pushService.SendAsync(@event.UserId, new PushNotification
        {
            Title = "Reminder",
            Body = @event.Title,
            Category = NotificationCategory.Reminder,
            Data = data
        }, cancellationToken);
    }
}
