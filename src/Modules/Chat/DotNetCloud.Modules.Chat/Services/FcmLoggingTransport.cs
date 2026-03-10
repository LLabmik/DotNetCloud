using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Default FCM transport that logs send attempts until HTTP integration is wired.
/// </summary>
internal sealed class FcmLoggingTransport : IFcmTransport
{
    private readonly ILogger<FcmLoggingTransport> _logger;

    public FcmLoggingTransport(ILogger<FcmLoggingTransport> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<FcmSendResult> SendAsync(DeviceRegistration device, PushNotification notification, CancellationToken cancellationToken = default)
    {
        // Placeholder for FCM HTTP v1 call integration.
        _logger.LogInformation(
            "FCM push to device {Token}: {Title}",
            device.Token[..Math.Min(8, device.Token.Length)],
            notification.Title);

        return Task.FromResult(FcmSendResult.Success);
    }
}
