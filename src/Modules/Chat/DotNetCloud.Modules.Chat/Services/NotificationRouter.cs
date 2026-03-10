using System.Collections.Concurrent;
using DotNetCloud.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Routes push notifications to the appropriate provider (FCM or UnifiedPush) based on
/// each user's registered device. Supports multiple devices per user, respects notification
/// preferences, and deduplicates when the user is online.
/// </summary>
internal sealed class NotificationRouter : IPushNotificationService
{
    private readonly IReadOnlyDictionary<PushProvider, IPushProviderEndpoint> _providers;
    private readonly INotificationPreferenceStore _preferenceStore;
    private readonly IPresenceTracker? _presenceTracker;
    private readonly ConcurrentDictionary<Guid, List<DeviceRegistration>> _deviceMap = new();
    private readonly ILogger<NotificationRouter> _logger;

    public NotificationRouter(
        IEnumerable<IPushProviderEndpoint> providers,
        INotificationPreferenceStore preferenceStore,
        ILogger<NotificationRouter> logger,
        IPresenceTracker? presenceTracker = null)
    {
        _providers = providers.ToDictionary(p => p.Provider, p => p);
        _preferenceStore = preferenceStore;
        _logger = logger;
        _presenceTracker = presenceTracker;
    }

    /// <inheritdoc />
    public async Task SendAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default)
    {
        if (!await CanSendPushAsync(userId, notification, cancellationToken))
        {
            return;
        }

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
            if (!_providers.TryGetValue(device.Provider, out var provider))
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
                _ when _providers.TryGetValue(registration.Provider, out var provider)
                    => provider.RegisterDeviceAsync(userId, registration, cancellationToken),
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
                    _ when _providers.TryGetValue(removed.Provider, out var provider)
                        => provider.UnregisterDeviceAsync(userId, deviceToken, cancellationToken),
                    _ => Task.CompletedTask
                };
            }
        }

        return Task.CompletedTask;
    }

    private async Task<bool> CanSendPushAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken)
    {
        var preferences = _preferenceStore.Get(userId);
        if (!preferences.PushEnabled)
        {
            _logger.LogDebug("Push disabled for user {UserId}; skipping notification", userId);
            return false;
        }

        if (preferences.DoNotDisturb)
        {
            _logger.LogDebug("Do-not-disturb enabled for user {UserId}; skipping notification", userId);
            return false;
        }

        if (IsMutedChannelNotification(notification, preferences.MutedChannelIds))
        {
            _logger.LogDebug("Channel muted for user {UserId}; skipping notification", userId);
            return false;
        }

        if (_presenceTracker is not null && await _presenceTracker.IsOnlineAsync(userId))
        {
            _logger.LogDebug("User {UserId} is online; suppressing push notification", userId);
            return false;
        }

        return true;
    }

    private static bool IsMutedChannelNotification(PushNotification notification, IReadOnlySet<Guid> mutedChannelIds)
    {
        if (notification.Category is not NotificationCategory.ChatMessage and not NotificationCategory.ChatMention)
        {
            return false;
        }

        if (!notification.Data.TryGetValue("channelId", out var channelIdRaw)
            && !notification.Data.TryGetValue("ChannelId", out channelIdRaw))
        {
            return false;
        }

        return Guid.TryParse(channelIdRaw, out var channelId) && mutedChannelIds.Contains(channelId);
    }
}
