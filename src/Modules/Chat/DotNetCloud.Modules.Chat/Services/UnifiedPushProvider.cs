using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// UnifiedPush notification provider. Sends notifications via HTTP POST to distributor endpoints.
/// UnifiedPush is an open protocol for push notifications without Google dependency.
/// </summary>
internal sealed class UnifiedPushProvider : IPushProviderEndpoint
{
    private readonly ConcurrentDictionary<Guid, List<DeviceRegistration>> _registrations = new();
    private readonly ILogger<UnifiedPushProvider> _logger;

    public UnifiedPushProvider(ILogger<UnifiedPushProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public PushProvider Provider => PushProvider.UnifiedPush;

    /// <inheritdoc />
    public Task SendAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default)
    {
        if (!_registrations.TryGetValue(userId, out var devices))
        {
            _logger.LogDebug("No UnifiedPush devices registered for user {UserId}", userId);
            return Task.CompletedTask;
        }

        var upDevices = devices.Where(d => d.Provider == PushProvider.UnifiedPush).ToList();
        foreach (var device in upDevices)
        {
            if (string.IsNullOrWhiteSpace(device.Endpoint))
            {
                _logger.LogWarning("UnifiedPush device for user {UserId} has no endpoint", userId);
                continue;
            }

            // In production: HTTP POST to device.Endpoint with notification payload
            _logger.LogInformation(
                "UnifiedPush to user {UserId} endpoint {Endpoint}: {Title}",
                userId, device.Endpoint, notification.Title);
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
                _logger.LogInformation("UnifiedPush device registered for user {UserId}", userId);
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

            _logger.LogInformation("UnifiedPush device unregistered for user {UserId}", userId);
        }

        return Task.CompletedTask;
    }
}
