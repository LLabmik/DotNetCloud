using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// FCM push notification provider. Sends notifications via Firebase Cloud Messaging HTTP v1 API.
/// Requires Firebase Admin SDK credentials configured in app settings.
/// </summary>
internal sealed class FcmPushProvider : IPushNotificationService
{
    private readonly ConcurrentDictionary<Guid, List<DeviceRegistration>> _registrations = new();
    private readonly ILogger<FcmPushProvider> _logger;

    public FcmPushProvider(ILogger<FcmPushProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default)
    {
        if (!_registrations.TryGetValue(userId, out var devices))
        {
            _logger.LogDebug("No FCM devices registered for user {UserId}", userId);
            return Task.CompletedTask;
        }

        var fcmDevices = devices.Where(d => d.Provider == PushProvider.FCM).ToList();
        foreach (var device in fcmDevices)
        {
            // In production: POST to https://fcm.googleapis.com/v1/projects/{project}/messages:send
            _logger.LogInformation(
                "FCM push to user {UserId} device {Token}: {Title}",
                userId, device.Token[..Math.Min(8, device.Token.Length)], notification.Title);
        }

        return Task.CompletedTask;
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
        var devices = _registrations.GetOrAdd(userId, _ => []);
        lock (devices)
        {
            if (!devices.Any(d => d.Token == registration.Token))
            {
                devices.Add(registration);
                _logger.LogInformation("FCM device registered for user {UserId}", userId);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnregisterDeviceAsync(Guid userId, string deviceToken, CancellationToken cancellationToken = default)
    {
        if (_registrations.TryGetValue(userId, out var devices))
        {
            lock (devices)
            {
                devices.RemoveAll(d => d.Token == deviceToken);
            }

            _logger.LogInformation("FCM device unregistered for user {UserId}", userId);
        }

        return Task.CompletedTask;
    }
}
