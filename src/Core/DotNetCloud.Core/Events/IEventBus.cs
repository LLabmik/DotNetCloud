namespace DotNetCloud.Core.Events;

using Authorization;

/// <summary>
/// Core interface for the event bus system.
/// Provides publish/subscribe functionality for inter-module communication within the DotNetCloud platform.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purpose:</b>
/// 
/// The event bus enables loosely-coupled, event-driven communication between modules.
/// Instead of modules calling each other directly, they communicate through domain events:
/// 
/// <list type="bullet">
///   <item><description>Module A publishes "UserCreatedEvent"</description></item>
///   <item><description>Modules B, C, and D receive the event (subscribed)</description></item>
///   <item><description>Each module independently reacts to the event</description></item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Benefits:</b>
/// 
/// <list type="bullet">
///   <item><description><b>Loose Coupling:</b> Modules don't need to know about each other</description></item>
///   <item><description><b>Scalability:</b> Easy to add new modules reacting to existing events</description></item>
///   <item><description><b>Resilience:</b> Failure in one handler doesn't cascade to others</description></item>
///   <item><description><b>Testability:</b> Modules can be tested independently</description></item>
///   <item><description><b>Maintainability:</b> Clear event-based contracts between modules</description></item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Usage Pattern:</b>
/// 
/// <code>
/// // 1. Define event
/// public sealed record UserCreatedEvent : IEvent
/// {
///     public Guid EventId { get; init; }
///     public DateTime CreatedAt { get; init; }
///     public Guid UserId { get; init; }
/// }
/// 
/// // 2. Publish event
/// var @event = new UserCreatedEvent
/// {
///     EventId = Guid.NewGuid(),
///     CreatedAt = DateTime.UtcNow,
///     UserId = newUserId
/// };
/// await _eventBus.PublishAsync(@event, callerContext, cancellationToken);
/// 
/// // 3. Subscribe handler
/// var handler = new UserCreatedEventHandler();
/// await _eventBus.SubscribeAsync(handler, cancellationToken);
/// 
/// // 4. Handle event (receives all published UserCreatedEvents)
/// public class UserCreatedEventHandler : IEventHandler&lt;UserCreatedEvent&gt;
/// {
///     public async Task HandleAsync(UserCreatedEvent @event, CancellationToken ct)
///     {
///         // React to user creation
///     }
/// }
/// </code>
/// </para>
/// 
/// <para>
/// <b>Publish/Subscribe Semantics:</b>
/// 
/// <list type="table">
///   <listheader>
///     <term>Aspect</term>
///     <description>Behavior</description>
///   </listheader>
///   <item>
///     <term>Publish Timing</term>
///     <description>
///       Fire-and-forget: PublishAsync returns after queuing, not after handler execution.
///       Handlers execute asynchronously in background.
///     </description>
///   </item>
///   <item>
///     <term>Delivery Guarantee</term>
///     <description>
///       Each registered handler receives each published event (at least once).
///       Handlers may receive duplicates in failure scenarios.
///     </description>
///   </item>
///   <item>
///     <term>Order Guarantee</term>
///     <description>
///       No ordering guarantee for events from different publishers.
///       Events from same publisher maintain order.
///     </description>
///   </item>
///   <item>
///     <term>Handler Execution</term>
///     <description>
///       Handlers execute asynchronously and may run concurrently.
///       Exception in one handler doesn't affect others.
///     </description>
///   </item>
///   <item>
///     <term>Subscription Scope</term>
///     <description>
///       Subscriptions exist until explicitly unsubscribed or system shutdown.
///       Persists across module lifecycle if not unsubscribed.
///     </description>
///   </item>
/// </list>
/// </para>
/// 
/// <para>
/// <b>Capability-Aware Subscriptions (Future):</b>
/// 
/// The event bus respects capability tiers. Modules can only subscribe to events
/// they have the necessary capabilities to receive. For example, a module without
/// IStorageProvider capability won't receive FileStorageEvents.
/// </para>
/// </remarks>
/// <seealso cref="IEvent"/>
/// <seealso cref="IEventHandler{TEvent}"/>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event asynchronously to all registered subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of event being published. Must implement <see cref="IEvent"/>.</typeparam>
    /// <param name="event">The event to publish. Must not be null.</param>
    /// <param name="caller">
    /// The context of the caller publishing the event.
    /// Used for authorization checks and auditing/logging.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the publish operation. Note: this cancels the publish operation itself,
    /// not necessarily the handler execution.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous publish operation. 
    /// Completes when event is queued, not when handlers complete.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Fire-and-Forget Semantics:</b>
    /// 
    /// This method returns immediately after queuing the event. It does not wait for
    /// handlers to execute. This allows the publisher to continue without blocking.
    /// </para>
    /// 
    /// <para>
    /// <b>Handler Execution:</b>
    /// 
    /// <list type="bullet">
    ///   <item><description>Handlers execute asynchronously after PublishAsync returns</description></item>
    ///   <item><description>Multiple handlers may execute concurrently</description></item>
    ///   <item><description>Exception in one handler doesn't affect other handlers</description></item>
    ///   <item><description>Handler exceptions are logged; not propagated to publisher</description></item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// <b>Best Practices:</b>
    /// 
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       Publish <b>after</b> persisting changes to database:
    ///       <code>
    ///       // Good: Publish after save
    ///       await _db.SaveChangesAsync(ct);
    ///       await _eventBus.PublishAsync(@event, caller, ct);
    ///       
    ///       // Bad: Publish before save (event lost if save fails)
    ///       await _eventBus.PublishAsync(@event, caller, ct);
    ///       await _db.SaveChangesAsync(ct);  // What if this fails?
    ///       </code>
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       Publish events that represent business facts, not implementation details
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       Don't include sensitive data in events (they're logged and audited)
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       Use unique EventId and current CreatedAt timestamp
    ///     </description>
    ///   </item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if @event or caller is null.
    /// </exception>
    Task PublishAsync<TEvent>(TEvent @event, CallerContext caller, CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>
    /// Subscribes an event handler to events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The type of events to subscribe to. Must implement <see cref="IEvent"/>.
    /// </typeparam>
    /// <param name="handler">
    /// The handler that will process events of type TEvent.
    /// Must not be null. Can be subscribed multiple times.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the subscribe operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous subscribe operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Subscription Timing:</b>
    /// 
    /// Typically called during module initialization:
    /// <code>
    /// public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken ct)
    /// {
    ///     var handler = new UserCreatedEventHandler();
    ///     await _eventBus.SubscribeAsync(handler, ct);
    /// }
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// <b>Multiple Subscriptions:</b>
    /// 
    /// A handler can be subscribed multiple times. Each subscription is independent:
    /// <code>
    /// var handler = new MyEventHandler();
    /// await _eventBus.SubscribeAsync(handler, ct);  // First subscription
    /// await _eventBus.SubscribeAsync(handler, ct);  // Second subscription
    /// // Handler will receive each event twice (once per subscription)
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// <b>Persistence:</b>
    /// 
    /// Subscriptions persist until:
    /// <list type="bullet">
    ///   <item><description>Explicitly unsubscribed via UnsubscribeAsync</description></item>
    ///   <item><description>System shutdown</description></item>
    ///   <item><description>Module stops</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if handler is null.
    /// </exception>
    Task SubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
        where TEvent : IEvent;

    /// <summary>
    /// Unsubscribes an event handler from events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">
    /// The type of events to unsubscribe from. Must implement <see cref="IEvent"/>.
    /// </typeparam>
    /// <param name="handler">
    /// The handler to unsubscribe. Must be the exact instance previously subscribed.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to cancel the unsubscribe operation.
    /// </param>
    /// <returns>
    /// A task representing the asynchronous unsubscribe operation.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Typical Usage:</b>
    /// 
    /// Called during module shutdown to clean up subscriptions:
    /// <code>
    /// public async Task StopAsync(CancellationToken cancellationToken)
    /// {
    ///     // Unsubscribe from all events
    ///     await _eventBus.UnsubscribeAsync(_handler1, cancellationToken);
    ///     await _eventBus.UnsubscribeAsync(_handler2, cancellationToken);
    /// }
    /// </code>
    /// </para>
    /// 
    /// <para>
    /// <b>Instance Identity:</b>
    /// 
    /// Only removes subscriptions for the exact handler instance provided.
    /// If you have multiple subscriptions of the same handler, call UnsubscribeAsync
    /// once - all subscriptions for that instance are removed.
    /// </para>
    /// 
    /// <para>
    /// <b>Idempotency:</b>
    /// 
    /// Unsubscribing a handler that was never subscribed (or already unsubscribed)
    /// has no effect and doesn't throw an exception.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if handler is null.
    /// </exception>
    Task UnsubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
        where TEvent : IEvent;
}
