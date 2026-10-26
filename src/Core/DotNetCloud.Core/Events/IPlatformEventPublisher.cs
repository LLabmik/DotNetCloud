namespace DotNetCloud.Core.Events;

/// <summary>
/// Publishes a platform event from a process-isolated module to the core, which owns the
/// notification, real-time and push delivery for it.
/// </summary>
/// <remarks>
/// <para>
/// A module host runs its own in-process <see cref="IEventBus"/>. Nothing inside the module process
/// consumes a platform delivery event, and the core cannot reach the module's bus, so a module that
/// delivered its own notifications had to invent an identity to do it. This interface is the
/// supported route instead: the module reports <i>what happened</i>, and the core decides who to
/// notify and over which channels.
/// </para>
/// <para>
/// There is deliberately <b>no caller parameter</b>. The publisher's identity is established by the
/// authenticated core transport (the supervisor-issued module credential), so a module can never
/// claim to be another module — or to be the platform itself — while asking the core to act.
/// </para>
/// <para>
/// Only event types the core registers as module-publishable are accepted. Publishing any other
/// type, or failing to reach the core, throws <see cref="Errors.PlatformEventPublishException"/>.
/// Callers must treat that as a real failure: a background job that has not published has not
/// notified anyone, and must not record the work as done.
/// </para>
/// </remarks>
public interface IPlatformEventPublisher
{
    /// <summary>
    /// Publishes <paramref name="event"/> to the core for platform-owned delivery.
    /// </summary>
    /// <typeparam name="TEvent">The platform event type. Its name is the wire contract.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>A task that completes when the core has accepted the event.</returns>
    /// <exception cref="Errors.PlatformEventPublishException">
    /// The event type is not module-publishable, or the core rejected or could not be reached.
    /// </exception>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
}
