namespace DotNetCloud.Core.Errors;

/// <summary>
/// Thrown when a module cannot hand a platform event to the core for delivery.
/// </summary>
/// <remarks>
/// Delivery of a platform notification (calendar reminder, share expiry warning, …) is owned by the
/// core. A module that fails to publish must treat the work as not done — it did not notify anyone —
/// rather than recording success and silently dropping the notification.
/// </remarks>
public sealed class PlatformEventPublishException : DotNetCloudException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformEventPublishException"/> class.
    /// </summary>
    /// <param name="eventType">The event type that could not be published.</param>
    /// <param name="message">The failure description.</param>
    public PlatformEventPublishException(string eventType, string message)
        : base(ErrorCodes.PlatformEventPublishFailed, message)
    {
        EventType = eventType;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlatformEventPublishException"/> class.
    /// </summary>
    /// <param name="eventType">The event type that could not be published.</param>
    /// <param name="message">The failure description.</param>
    /// <param name="innerException">The underlying transport or serialization failure.</param>
    public PlatformEventPublishException(string eventType, string message, Exception innerException)
        : base(ErrorCodes.PlatformEventPublishFailed, message, innerException)
    {
        EventType = eventType;
    }

    /// <summary>
    /// Gets the event type that could not be published.
    /// </summary>
    public string EventType { get; }
}
