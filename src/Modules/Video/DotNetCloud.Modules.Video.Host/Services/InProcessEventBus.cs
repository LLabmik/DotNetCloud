using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;

namespace DotNetCloud.Modules.Video.Host.Services;

/// <summary>
/// Simple in-process event bus for standalone module operation.
/// In production, the core server provides its own implementation via gRPC.
/// </summary>
internal sealed class InProcessEventBus : IEventBus
{
    private readonly List<object> _handlers = [];
    private readonly ILogger<InProcessEventBus> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InProcessEventBus"/> class.
    /// </summary>
    public InProcessEventBus(ILogger<InProcessEventBus> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CallerContext caller, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
