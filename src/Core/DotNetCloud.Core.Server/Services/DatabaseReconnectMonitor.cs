using DotNetCloud.Core.ServiceDefaults.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Server.Services;

/// <summary>
/// Background service that periodically probes the database and updates
/// <see cref="DatabaseConnectivityState"/>. This is the automatic reconnect
/// mechanism: when the DB comes back, the state flips to available and the
/// 503 gate re-opens without a process restart.
/// </summary>
public sealed class DatabaseReconnectMonitor : BackgroundService
{
    private static readonly TimeSpan DownPollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UpPollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly DatabaseConnectivityState _state;
    private readonly ILogger<DatabaseReconnectMonitor> _logger;

    /// <summary>Creates a new reconnect monitor.</summary>
    public DatabaseReconnectMonitor(
        IDbConnectionFactory connectionFactory,
        DatabaseConnectivityState state,
        ILogger<DatabaseReconnectMonitor> logger)
    {
        _connectionFactory = connectionFactory;
        _state = state;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var available = await ProbeAsync(stoppingToken).ConfigureAwait(false);
            var was = _state.IsAvailable;
            _state.SetAvailable(available);

            if (was && !available)
                _logger.LogCritical("Database connectivity lost. Requests requiring the database will return 503 until it recovers.");
            else if (!was && available)
                _logger.LogInformation("Database connectivity restored. Resuming normal operation.");

            await Task.Delay(available ? UpPollInterval : DownPollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> ProbeAsync(CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(cts.Token).ConfigureAwait(false);
            await connection.OpenAsync(cts.Token).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Database availability probe failed.");
            return false;
        }
    }
}
