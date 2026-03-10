using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// UnifiedPush notification provider. Sends notifications via HTTP POST to distributor endpoints.
/// UnifiedPush is an open protocol for push notifications without Google dependency.
/// </summary>
internal sealed class UnifiedPushProvider : IPushProviderEndpoint
{
    private const int MaxSendAttempts = 3;

    private readonly ConcurrentDictionary<Guid, List<DeviceRegistration>> _registrations = new();
    private readonly IUnifiedPushTransport _transport;
    private readonly ILogger<UnifiedPushProvider> _logger;

    public UnifiedPushProvider(IUnifiedPushTransport transport, ILogger<UnifiedPushProvider> logger)
    {
        _transport = transport;
        _logger = logger;
    }

    /// <inheritdoc />
    public PushProvider Provider => PushProvider.UnifiedPush;

    /// <inheritdoc />
    public async Task SendAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default)
    {
        if (!_registrations.TryGetValue(userId, out var devices))
        {
            _logger.LogDebug("No UnifiedPush devices registered for user {UserId}", userId);
            return;
        }

        var upDevices = devices.Where(d => d.Provider == PushProvider.UnifiedPush).ToList();
        foreach (var device in upDevices)
        {
            if (string.IsNullOrWhiteSpace(device.Endpoint))
            {
                _logger.LogWarning("UnifiedPush device for user {UserId} has no endpoint", userId);
                continue;
            }

            var delivered = false;
            for (var attempt = 1; attempt <= MaxSendAttempts; attempt++)
            {
                var result = await _transport.SendAsync(device.Endpoint, notification, cancellationToken);
                if (result.IsSuccess)
                {
                    delivered = true;
                    break;
                }

                if (!result.IsTransientFailure)
                {
                    _logger.LogWarning(
                        "UnifiedPush non-retryable failure for user {UserId} endpoint {Endpoint}: {Error}",
                        userId,
                        device.Endpoint,
                        result.Error ?? "unknown_error");
                    break;
                }

                _logger.LogWarning(
                    "UnifiedPush transient failure attempt {Attempt}/{MaxAttempts} for user {UserId} endpoint {Endpoint}: {Error}",
                    attempt,
                    MaxSendAttempts,
                    userId,
                    device.Endpoint,
                    result.Error ?? "transient_error");
            }

            if (!delivered)
            {
                _logger.LogWarning("UnifiedPush delivery exhausted retries for user {UserId} endpoint {Endpoint}", userId, device.Endpoint);
            }
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
