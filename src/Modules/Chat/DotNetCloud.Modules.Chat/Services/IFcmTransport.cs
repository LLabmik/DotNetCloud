namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Abstraction for dispatching FCM push payloads.
/// </summary>
internal interface IFcmTransport
{
    /// <summary>
    /// Sends a push notification to one FCM device.
    /// </summary>
    Task<FcmSendResult> SendAsync(DeviceRegistration device, PushNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends push notifications to multiple devices concurrently for efficiency.
    /// </summary>
    Task<IReadOnlyList<FcmSendResult>> SendBatchAsync(IReadOnlyList<(DeviceRegistration Device, PushNotification Notification)> messages, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a single FCM send attempt.
/// </summary>
internal sealed record FcmSendResult
{
    /// <summary>
    /// Gets a success result.
    /// </summary>
    public static FcmSendResult Success { get; } = new() { IsSuccess = true };

    /// <summary>
    /// Gets a result indicating an invalid token that should be cleaned up.
    /// </summary>
    public static FcmSendResult InvalidToken { get; } = new() { IsInvalidToken = true, Error = "invalid_token" };

    /// <summary>
    /// Gets or sets a value indicating whether send succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether token is invalid and should be removed.
    /// </summary>
    public bool IsInvalidToken { get; init; }

    /// <summary>
    /// Gets or sets an error message for logging.
    /// </summary>
    public string? Error { get; init; }
}
