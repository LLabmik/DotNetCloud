using DotNetCloud.Modules.Chat.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat.Data.Services;

/// <summary>
/// Periodically applies the admin-configured chat retention policy.
/// </summary>
/// <remarks>
/// <para>
/// Only the process-isolated Chat module host registers this service, so the sweep runs exactly
/// once per deployment even though Core.Server also builds an in-process chat container for the
/// Blazor UI.
/// </para>
/// <para>
/// The interval is re-read from the settings on every pass, so changing
/// <see cref="ChatSettingKeys.SweepIntervalMinutes"/> (or disabling retention entirely) takes
/// effect on the next tick without restarting the module.
/// </para>
/// </remarks>
internal sealed class ChatRetentionBackgroundService : BackgroundService
{
    /// <summary>Delay before the first sweep, giving migrations and health checks a head start.</summary>
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(60);

    /// <summary>Interval used when the sweep itself fails, so a broken pass does not spin.</summary>
    private static readonly TimeSpan FailureRetryInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChatRetentionBackgroundService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatRetentionBackgroundService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create a scope per sweep.</param>
    /// <param name="logger">Logger instance.</param>
    public ChatRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ChatRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Chat retention background service started.");

        if (await DelayAsync(InitialDelay, stoppingToken).ConfigureAwait(false) is false)
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = FailureRetryInterval;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var settingsProvider = scope.ServiceProvider.GetRequiredService<IChatSettingsProvider>();
                var settings = await settingsProvider.GetSettingsAsync(stoppingToken).ConfigureAwait(false);

                interval = TimeSpan.FromMinutes(settings.SweepIntervalMinutes);

                if (settings.HasRetentionPolicy)
                {
                    var retentionService = scope.ServiceProvider.GetRequiredService<IChatRetentionService>();
                    await retentionService.SweepAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat retention sweep failed; retrying in {Minutes} minute(s).",
                    FailureRetryInterval.TotalMinutes);
            }

            if (await DelayAsync(interval, stoppingToken).ConfigureAwait(false) is false)
                break;
        }

        _logger.LogInformation("Chat retention background service stopped.");
    }

    /// <summary>Waits for the given duration; returns false when shutdown was requested.</summary>
    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
