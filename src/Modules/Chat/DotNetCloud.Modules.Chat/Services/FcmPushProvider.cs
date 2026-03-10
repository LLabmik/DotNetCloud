using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// FCM push notification provider. Sends notifications via Firebase Cloud Messaging HTTP v1 API.
/// Requires Firebase Admin SDK credentials configured in app settings.
/// </summary>
internal sealed class FcmPushProvider : IPushProviderEndpoint
{
    private readonly ConcurrentDictionary<Guid, List<DeviceRegistration>> _registrations = new();
    private readonly IFcmTransport _transport;
    private readonly FcmPushOptions _options;
    private readonly ILogger<FcmPushProvider> _logger;

    public FcmPushProvider(
        IFcmTransport transport,
        IOptions<FcmPushOptions> options,
        ILogger<FcmPushProvider> logger)
    {
        _transport = transport;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public PushProvider Provider => PushProvider.FCM;

    /// <inheritdoc />
    public async Task SendAsync(Guid userId, PushNotification notification, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("FCM provider disabled; skipping push delivery");
            return;
        }

        if (!_registrations.TryGetValue(userId, out var devices))
        {
            _logger.LogDebug("No FCM devices registered for user {UserId}", userId);
            return;
        }

        var fcmDevices = devices.Where(d => d.Provider == PushProvider.FCM).ToList();
        var invalidTokens = new List<string>();

        foreach (var device in fcmDevices)
        {
            var result = await _transport.SendAsync(device, notification, cancellationToken);
            if (result.IsInvalidToken)
            {
                invalidTokens.Add(device.Token);
                _logger.LogWarning("FCM invalid token cleanup scheduled for user {UserId}", userId);
                continue;
            }

            if (!result.IsSuccess)
            {
                _logger.LogWarning("FCM send failed for user {UserId}: {Error}", userId, result.Error ?? "unknown_error");
            }
        }

        if (invalidTokens.Count > 0)
        {
            lock (devices)
            {
                devices.RemoveAll(d => invalidTokens.Contains(d.Token));
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
