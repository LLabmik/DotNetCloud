namespace DotNetCloud.Core.Events;

using Authorization;

/// <summary>
/// Core interface for the event bus system.
/// Provides publish/subscribe functionality for inter-module communication within the DotNetCloud platform.
/// </summary>
/// <remarks>
/// The event bus enables loosely-coupled communication between modules.
/// Modules can publish events without knowing which other modules are listening,
/// and subscribe to events without knowing which modules produce them.
/// 
/// The event bus respects capability tiers - modules can only subscribe to events
/// they have the necessary capabilities to receive.
/// </remarks>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event asynchronously to all registered subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of event being published. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="caller">The context of the caller publishing the event. Used for authorization and auditing.</param>
    /// <param name="cancellationToken">A token to cancel the publish operation.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    /// <remarks>
    /// Publish is a fire-and-forget operation. The method returns once the event has been
    /// queued for delivery to subscribers. Handlers are executed asynchronously.
    /// 
    /// If a handler throws an exception, the event bus logs the error but continues
    /// processing other handlers for the same event.
    /// </remarks>
    Task PublishAsync<TEvent>(TEvent @event, CallerContext caller, CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>
    /// Subscribes an event handler to events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">The type of events to subscribe to. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="handler">The handler that will process events.</param>
    /// <param name="cancellationToken">A token to cancel the subscribe operation.</param>
    /// <returns>A task representing the asynchronous subscribe operation.</returns>
    /// <remarks>
    /// A single handler can be subscribed multiple times. Each subscription is independent.
    /// Subscriptions are typically made during module initialization.
    /// 
    /// The subscription persists until explicitly unsubscribed or the handler is disposed.
    /// </remarks>
    Task SubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>
    /// Unsubscribes an event handler from events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">The type of events to unsubscribe from. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="handler">The handler to unsubscribe.</param>
    /// <param name="cancellationToken">A token to cancel the unsubscribe operation.</param>
    /// <returns>A task representing the asynchronous unsubscribe operation.</returns>
    /// <remarks>
    /// Only removes subscriptions for the exact handler instance provided.
    /// If the handler was subscribed multiple times, all subscriptions are removed.
    /// Unsubscribing a handler that was never subscribed has no effect.
    /// </remarks>
    Task UnsubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
        where TEvent : IEvent;
}
