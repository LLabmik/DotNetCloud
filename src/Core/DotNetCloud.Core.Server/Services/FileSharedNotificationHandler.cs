using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Services;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Sends push notifications when a file is shared with a user.
/// </summary>
internal sealed class FileSharedNotificationHandler : IEventHandler<FileSharedEvent>
{
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<FileSharedNotificationHandler> _logger;

    public FileSharedNotificationHandler(
        IPushNotificationService pushService,
        ILogger<FileSharedNotificationHandler> logger)
    {
        _pushService = pushService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(FileSharedEvent @event, CancellationToken cancellationToken = default)
    {
        // Only send notification for user-targeted shares (not public links)
        if (@event.SharedWithUserId is null)
            return;

        _logger.LogInformation("Sending share notification to user {UserId} for file {FileName}",
            @event.SharedWithUserId, @event.FileName);

        await _pushService.SendAsync(@event.SharedWithUserId.Value, new PushNotification
        {
            Title = "File shared with you",
            Body = $"\"{@event.FileName}\" has been shared with you.",
            Category = NotificationCategory.FileShared,
            Data = new Dictionary<string, string>
            {
                ["fileNodeId"] = @event.FileNodeId.ToString(),
                ["shareId"] = @event.ShareId.ToString()
            }
        }, cancellationToken);
    }
}
