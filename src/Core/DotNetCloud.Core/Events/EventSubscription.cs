namespace DotNetCloud.Core.Events;

/// <summary>
/// Represents a subscription of an event handler to a specific event type.
/// Used internally by the event bus to track and manage subscriptions.
/// </summary>
public sealed record EventSubscription
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventSubscription"/> record.
    /// </summary>
    /// <param name="subscriptionId">Unique identifier for this subscription.</param>
    /// <param name="eventType">The type of event this subscription handles.</param>
    /// <param name="handlerType">The type of the handler processing events.</param>
    /// <param name="moduleId">The module ID that owns this subscription.</param>
    /// <param name="subscribedAt">The date and time when the subscription was created.</param>
    /// <exception cref="ArgumentException">Thrown when subscriptionId is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when eventType, handlerType, or moduleId is null or empty.</exception>
    public EventSubscription(
        Guid subscriptionId,
        Type eventType,
        Type handlerType,
        string moduleId,
        DateTime subscribedAt)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(handlerType);
        if (string.IsNullOrWhiteSpace(moduleId))
            throw new ArgumentException("Module ID cannot be null or empty.", nameof(moduleId));

        SubscriptionId = subscriptionId;
        EventType = eventType;
        HandlerType = handlerType;
        ModuleId = moduleId;
        SubscribedAt = subscribedAt;
    }

    /// <summary>
    /// Gets the unique identifier for this subscription.
    /// </summary>
    public Guid SubscriptionId { get; }

    /// <summary>
    /// Gets the type of event this subscription handles.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// Gets the type of the event handler.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// Gets the module ID that owns this subscription.
    /// </summary>
    public string ModuleId { get; }

    /// <summary>
    /// Gets the date and time when this subscription was created.
    /// </summary>
    public DateTime SubscribedAt { get; }
}
