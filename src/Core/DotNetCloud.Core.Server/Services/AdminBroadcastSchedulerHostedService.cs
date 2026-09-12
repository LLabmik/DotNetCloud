using DotNetCloud.Core.Services;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Background job that delivers scheduled administrator broadcasts whose time has arrived.
/// </summary>
/// <remarks>
/// <para>
/// Polls every 30 seconds so a scheduled broadcast becomes visible to connected users
/// shortly after its send time. Delivery itself is handled by
/// <see cref="IAdminBroadcastService.PublishPendingAsync"/>, which pushes the message to
/// the Blazor circuit relays over SignalR.
/// </para>
/// <para>
/// Failures (for example a temporary database outage) are logged and retried on the next
/// tick rather than stopping the service — the broadcast rows remain pending until delivered.
/// </para>
/// </remarks>
public sealed class AdminBroadcastSchedulerHostedService : BackgroundService
{
    private const string ServiceName = "Admin Broadcast Scheduler";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundServiceTracker _tracker;
    private readonly ILogger<AdminBroadcastSchedulerHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminBroadcastSchedulerHostedService"/> class.
    /// </summary>
    public AdminBroadcastSchedulerHostedService(
        IServiceScopeFactory scopeFactory,
        IBackgroundServiceTracker tracker,
        ILogger<AdminBroadcastSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{ServiceName} started.", ServiceName);

        // Allow the host (and database) to finish starting before the first pass.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var ok = true;
            var message = string.Empty;

            try
            {
                var published = await PublishDueBroadcastsAsync(stoppingToken);
                message = published == 0 ? "No due broadcasts" : $"Delivered {published} broadcast(s)";
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ok = false;
                message = ex.Message;
                _logger.LogError(ex, "{ServiceName} failed during delivery pass.", ServiceName);
            }

            stopwatch.Stop();
            _tracker.RecordRun(ServiceName, DateTimeOffset.UtcNow, stopwatch.Elapsed, ok, message);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("{ServiceName} stopped.", ServiceName);
    }

    /// <summary>
    /// Delivers every scheduled broadcast that is due.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of broadcasts delivered.</returns>
    public async Task<int> PublishDueBroadcastsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAdminBroadcastService>();
        return await service.PublishPendingAsync(cancellationToken);
    }
}
