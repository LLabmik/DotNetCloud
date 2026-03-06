using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Simple in-process event bus used by in-process module services.
/// </summary>
internal sealed class InProcessEventBus : IEventBus
{
    private readonly List<object> _handlers = [];
    private readonly ILogger<InProcessEventBus> _logger;

    public InProcessEventBus(ILogger<InProcessEventBus> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CallerContext caller, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(caller);

        List<IEventHandler<TEvent>> snapshot;
        lock (_handlers)
        {
            snapshot = _handlers.OfType<IEventHandler<TEvent>>().ToList();
        }

        foreach (var handler in snapshot)
        {
            try
            {
                await handler.HandleAsync(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Event handler {Handler} failed for {Event}", handler.GetType().Name, typeof(TEvent).Name);
            }
        }
    }

    public Task SubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_handlers)
        {
            _handlers.Add(handler);
        }

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_handlers)
        {
            _handlers.Remove(handler);
        }

        return Task.CompletedTask;
    }
}
