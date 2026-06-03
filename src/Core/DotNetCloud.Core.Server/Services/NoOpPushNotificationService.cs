using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// No-op implementation of <see cref="IPushNotificationService"/> for use by Core.Server.
/// Push notifications are handled by the Chat module's gRPC service.
/// </summary>
internal sealed class NoOpPushNotificationService : IPushNotificationService
{
    private readonly ILogger<NoOpPushNotificationService> _logger;

    public NoOpPushNotificationService(ILogger<NoOpPushNotificationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Push notification to user {UserId}: {Title}", userId, notification.Title);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendToMultipleAsync(IEnumerable<Guid> userIds, PushNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Push notification to {Count} users: {Title}", userIds.Count(), notification.Title);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RegisterDeviceAsync(Guid userId, DeviceRegistration registration, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnregisterDeviceAsync(Guid userId, string deviceToken, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
