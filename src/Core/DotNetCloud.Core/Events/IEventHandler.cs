namespace DotNetCloud.Core.Events;

/// <summary>
/// Generic interface for handling events of a specific type.
/// Modules implement this interface to subscribe to and process events.
/// </summary>
/// <typeparam name="TEvent">The specific event type this handler processes. Must implement <see cref="IEvent"/>.</typeparam>
public interface IEventHandler<in TEvent> where TEvent : IEvent
{
    /// <summary>
    /// Handles the specified event asynchronously.
    /// Implementations should be idempotent, as events may be replayed or delivered multiple times.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">A token to cancel the handler operation.</param>
    /// <returns>A task representing the asynchronous handling operation.</returns>
    /// <remarks>
    /// Handlers should complete quickly. Long-running operations should be dispatched to background jobs.
    /// Exceptions thrown by handlers should be caught and logged by the event bus.
    /// </remarks>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
