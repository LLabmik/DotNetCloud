using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Starts an account-erasure request whenever a user is deleted.
/// </summary>
/// <remarks>
/// <para>
/// <c>UserManagementService</c> publishes <see cref="UserDeletedEvent"/> once the identity row is
/// gone. Before phase A3 that event had no live subscriber, so a deleted user's data stayed behind
/// in every module process. This subscriber closes that gap: it creates the
/// <c>UserErasureRequest</c> and lets <see cref="UserErasureRetryHostedService"/> do the delivery,
/// so a module that is offline during the deletion is retried rather than forgotten.
/// </para>
/// <para>
/// The handler never throws: a failure to enqueue would otherwise surface as a failed account
/// deletion after the account row is already gone.
/// </para>
/// </remarks>
internal sealed class UserDeletedEventSubscriber : IHostedService
{
    private readonly IEventBus _eventBus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserDeletedEventSubscriber> _logger;
    private UserDeletedHandler? _handler;

    public UserDeletedEventSubscriber(
        IEventBus eventBus,
        IServiceScopeFactory scopeFactory,
        ILogger<UserDeletedEventSubscriber> logger)
    {
        _eventBus = eventBus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _handler = new UserDeletedHandler(_scopeFactory, _logger);
        await _eventBus.SubscribeAsync<UserDeletedEvent>(_handler, cancellationToken);
        _logger.LogInformation("User-erasure subscriber started.");
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_handler is null)
        {
            return;
        }

        try
        {
            await _eventBus.UnsubscribeAsync<UserDeletedEvent>(_handler, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error unsubscribing the user-erasure handler.");
        }
    }

    private sealed class UserDeletedHandler : IEventHandler<UserDeletedEvent>
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;

        public UserDeletedHandler(IServiceScopeFactory scopeFactory, ILogger logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task HandleAsync(UserDeletedEvent @event, CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var erasure = scope.ServiceProvider.GetRequiredService<IUserErasureService>();

                var request = await erasure.RequestErasureAsync(
                    @event.UserId,
                    requestedByUserId: null,
                    cancellationToken);

                // Deliver immediately; the retry loop covers anything left outstanding.
                await erasure.ProcessPendingAsync(cancellationToken);

                _logger.LogInformation(
                    "User erasure {RequestId} created for deleted user {UserId} ({ModuleCount} module(s)).",
                    request.RequestId,
                    @event.UserId,
                    request.Modules.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to enqueue user erasure for deleted user {UserId}; an administrator must re-run it.",
                    @event.UserId);
            }
        }
    }
}

/// <summary>
/// Retries outstanding account-erasure deliveries until they are acknowledged or expire.
/// </summary>
internal sealed class UserErasureRetryHostedService : BackgroundService
{
    /// <summary>How often outstanding erasures are retried.</summary>
    internal static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserErasureRetryHostedService> _logger;

    public UserErasureRetryHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<UserErasureRetryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RetryInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var erasure = scope.ServiceProvider.GetRequiredService<IUserErasureService>();
                    await erasure.ProcessPendingAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "User-erasure retry pass failed; it will run again.");
            }
        }
    }
}
