using System.Threading.Channels;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Services.ModuleApis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Buffers newly created accounts and hands them to the provisioning drain.
/// </summary>
/// <remarks>
/// The subscriber must never block registration: the event handler only enqueues a user id, and a
/// bounded channel sheds load if the Files module is unavailable for a long time. The start-up
/// convergence sweep is the safety net for anything that is dropped.
/// </remarks>
internal sealed class UserContentProvisioningDispatcher : IEventHandler<UserCreatedEvent>
{
    private const int Capacity = 10_000;

    private readonly Channel<Guid> _channel;
    private readonly ILogger<UserContentProvisioningDispatcher> _logger;

    public UserContentProvisioningDispatcher(ILogger<UserContentProvisioningDispatcher> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        });
    }

    /// <summary>Reader consumed by the hosted drain.</summary>
    public ChannelReader<Guid> Reader => _channel.Reader;

    /// <inheritdoc />
    public Task HandleAsync(UserCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        if (!_channel.Writer.TryWrite(@event.UserId))
        {
            _logger.LogWarning(
                "Default-content queue is full; user {UserId} will be picked up by the convergence sweep.",
                @event.UserId);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Drains <see cref="UserContentProvisioningDispatcher"/> and calls the Files module.
/// </summary>
internal sealed class UserContentProvisioningDispatcherHostedService : IHostedService
{
    private const int WorkerCount = 2;
    private const int MaxAttempts = 3;

    private readonly UserContentProvisioningDispatcher _dispatcher;
    private readonly IEventBus _eventBus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserContentProvisioningDispatcherHostedService> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private Task[] _workers = [];

    public UserContentProvisioningDispatcherHostedService(
        UserContentProvisioningDispatcher dispatcher,
        IEventBus eventBus,
        IServiceScopeFactory scopeFactory,
        ILogger<UserContentProvisioningDispatcherHostedService> logger)
    {
        _dispatcher = dispatcher;
        _eventBus = eventBus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _eventBus.SubscribeAsync<UserCreatedEvent>(_dispatcher, cancellationToken);

        _workers = Enumerable.Range(0, WorkerCount)
            .Select(_ => Task.Run(() => DrainAsync(_stopping.Token), CancellationToken.None))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _eventBus.UnsubscribeAsync<UserCreatedEvent>(_dispatcher, cancellationToken);
        await _stopping.CancelAsync();

        try
        {
            await Task.WhenAll(_workers).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var userId in _dispatcher.Reader.ReadAllAsync(cancellationToken))
            {
                await ProvisionAsync(userId, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Default-content dispatcher stopped unexpectedly.");
        }
    }

    private async Task ProvisionAsync(Guid userId, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ProvisionUserDefaultsResult result;

            using (var scope = _scopeFactory.CreateScope())
            {
                var client = scope.ServiceProvider.GetService<IFilesApiClient>();
                if (client is null)
                {
                    _logger.LogDebug(
                        "Files API client is not registered; user {UserId} will be provisioned by the sweep.",
                        userId);
                    return;
                }

                result = await client.ProvisionUserDefaultsAsync(userId, cancellationToken);
            }

            if (result.Success)
            {
                _logger.LogInformation(
                    "Provisioned default content for {UserId}: {Status} ({Created} item(s)).",
                    userId, result.Status, result.ItemsCreated);
                return;
            }

            if (attempt == MaxAttempts)
            {
                _logger.LogWarning(
                    "Default-content provisioning for {UserId} failed after {Attempts} attempt(s): {Error}. " +
                    "The convergence sweep will retry.",
                    userId, attempt, result.ErrorMessage);
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5 * attempt), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
