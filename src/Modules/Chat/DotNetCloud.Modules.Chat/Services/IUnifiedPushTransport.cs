namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Abstraction for dispatching UnifiedPush payloads to distributor endpoints.
/// </summary>
internal interface IUnifiedPushTransport
{
    /// <summary>
    /// Sends a push notification to one UnifiedPush endpoint.
    /// </summary>
    Task<UnifiedPushSendResult> SendAsync(string endpoint, PushNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a UnifiedPush send attempt.
/// </summary>
internal sealed record UnifiedPushSendResult
{
    /// <summary>
    /// Gets a success result.
    /// </summary>
    public static UnifiedPushSendResult Success { get; } = new() { IsSuccess = true };

    /// <summary>
    /// Gets or sets a value indicating whether send succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the failure is transient and retryable.
    /// </summary>
    public bool IsTransientFailure { get; init; }

    /// <summary>
    /// Gets or sets an error message for logging.
    /// </summary>
    public string? Error { get; init; }
}
