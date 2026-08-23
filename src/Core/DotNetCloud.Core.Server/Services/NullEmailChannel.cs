using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Placeholder email channel. Implement real email delivery later via
/// IEmailApiClient + IUserDirectory + the user's "notifications.Email" setting.
/// </summary>
internal sealed class NullEmailChannel : INotificationChannel
{
    private readonly ILogger<NullEmailChannel> _logger;

    public NullEmailChannel(ILogger<NullEmailChannel> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task DeliverAsync(NotificationDto notification, CancellationToken ct = default)
    {
        _logger.LogDebug("Email channel not implemented; skipping notification {NotificationId}", notification.Id);
        return Task.CompletedTask;
    }
}
