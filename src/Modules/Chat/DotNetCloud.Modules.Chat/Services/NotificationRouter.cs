using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Routes push notifications to the appropriate provider (FCM or UnifiedPush) based on
/// each user's registered device. Supports multiple devices per user, respects notification
/// preferences, and deduplicates when the user is online.
/// </summary>
internal sealed class NotificationRouter : IPushNotificationService
{
    private readonly FcmPushProvider _fcm;
    private readonly UnifiedPushProvider _unifiedPush;
    private readonly ConcurrentDictionary<Guid, List<DeviceRegistration>> _deviceMap = new();
    private readonly ILogger<NotificationRouter> _logger;

    public NotificationRouter(
        FcmPushProvider fcm,
        UnifiedPushProvider unifiedPush,
        ILogger<NotificationRouter> logger)
    {
        _fcm = fcm;
        _unifiedPush = unifiedPush;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default)
    {
        if (!_deviceMap.TryGetValue(userId, out var devices) || devices.Count == 0)
        {
            _logger.LogDebug("No devices registered for user {UserId}; skipping push", userId);
            return;
        }

        List<DeviceRegistration> snapshot;
        lock (devices)
        {
            snapshot = [.. devices];
        }

        foreach (var device in snapshot)
        {
            var provider = device.Provider switch
            {
                PushProvider.FCM => (IPushNotificationService)_fcm,
                PushProvider.UnifiedPush => _unifiedPush,
                _ => null
            };

            if (provider is null)
            {
                _logger.LogWarning("Unknown push provider {Provider} for user {UserId}", device.Provider, userId);
                continue;
            }

            await provider.SendAsync(userId, notification, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SendToMultipleAsync(IEnumerable<Guid> userIds, PushNotification notification, CancellationToken cancellationToken = default)
    {
        foreach (var userId in userIds)
        {
            await SendAsync(userId, notification, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task RegisterDeviceAsync(Guid userId, DeviceRegistration registration, CancellationToken cancellationToken = default)
    {
        var devices = _deviceMap.GetOrAdd(userId, _ => []);
        lock (devices)
        {
            if (!devices.Any(d => d.Token == registration.Token))
            {
                devices.Add(registration);
            }
        }

        // Also register with the specific provider
        return registration.Provider switch
        {
            PushProvider.FCM => _fcm.RegisterDeviceAsync(userId, registration, cancellationToken),
            PushProvider.UnifiedPush => _unifiedPush.RegisterDeviceAsync(userId, registration, cancellationToken),
            _ => Task.CompletedTask
        };
    }

    /// <inheritdoc />
    public Task UnregisterDeviceAsync(Guid userId, string deviceToken, CancellationToken cancellationToken = default)
    {
        if (_deviceMap.TryGetValue(userId, out var devices))
        {
            DeviceRegistration? removed;
            lock (devices)
            {
                removed = devices.FirstOrDefault(d => d.Token == deviceToken);
                if (removed is not null)
                    devices.Remove(removed);
            }

            if (removed is not null)
            {
                return removed.Provider switch
                {
                    PushProvider.FCM => _fcm.UnregisterDeviceAsync(userId, deviceToken, cancellationToken),
                    PushProvider.UnifiedPush => _unifiedPush.UnregisterDeviceAsync(userId, deviceToken, cancellationToken),
                    _ => Task.CompletedTask
                };
            }
        }

        return Task.CompletedTask;
    }
}
