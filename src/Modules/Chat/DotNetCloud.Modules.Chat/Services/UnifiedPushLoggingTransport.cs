using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Default UnifiedPush transport that logs delivery attempts until HTTP integration is wired.
/// </summary>
internal sealed class UnifiedPushLoggingTransport : IUnifiedPushTransport
{
    private readonly ILogger<UnifiedPushLoggingTransport> _logger;

    public UnifiedPushLoggingTransport(ILogger<UnifiedPushLoggingTransport> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<UnifiedPushSendResult> SendAsync(string endpoint, PushNotification notification, CancellationToken cancellationToken = default)
    {
        // Placeholder for HTTP POST distributor integration.
        _logger.LogInformation("UnifiedPush to endpoint {Endpoint}: {Title}", endpoint, notification.Title);
        return Task.FromResult(UnifiedPushSendResult.Success);
    }
}
