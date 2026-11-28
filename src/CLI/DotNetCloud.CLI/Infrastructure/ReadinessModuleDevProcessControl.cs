using System.Net;
using System.Text.Json;
using DotNetCloud.ModulePackaging;

namespace DotNetCloud.CLI.Infrastructure;

/// <summary>
/// Stops and starts a module through the running server's readiness report.
/// </summary>
/// <remarks>
/// <para>
/// The CLI never spawns a module and never sees the supervisor's process table; the supervisor owns
/// every module process. So the deploy writes the lifecycle state into the database
/// (<c>Disabled</c>/<c>Enabled</c>) and this class waits until the server reports the consequence.
/// </para>
/// <para>
/// The observation channel is <c>GET /health/ready</c>: it is anonymous, it is fed by the aggregate
/// module health check, and it already reports each module's <c>processStatus</c> and
/// <c>status</c> — the same two facts the deploy needs. No admin token, no second port, no new
/// endpoint.
/// </para>
/// </remarks>
internal sealed class ReadinessModuleDevProcessControl : IModuleDevProcessControl
{
    private const string AggregateEntryName = "modules-aggregate";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    private readonly HttpClient _client;
    private readonly TimeSpan _stopTimeout;
    private readonly TimeSpan _startTimeout;

    /// <summary>Initializes a new instance of the <see cref="ReadinessModuleDevProcessControl"/> class.</summary>
    /// <param name="serverBaseUrl">The running server, for example <c>http://localhost:5080</c>.</param>
    /// <param name="stopTimeout">How long a stop may take (reconcile interval plus graceful shutdown).</param>
    /// <param name="startTimeout">How long a start may take (the supervisor's startup timeout).</param>
    public ReadinessModuleDevProcessControl(string serverBaseUrl, TimeSpan stopTimeout, TimeSpan startTimeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverBaseUrl);

        _client = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri(serverBaseUrl),
            Timeout = TimeSpan.FromSeconds(30),
        };
        _stopTimeout = stopTimeout;
        _startTimeout = startTimeout;
    }

    /// <summary>
    /// Checks whether the server answers, so <c>dev</c> fails before touching anything.
    /// </summary>
    /// <returns><see langword="null"/> when it answers, otherwise the reason.</returns>
    public async Task<string?> CheckServerAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            using var response = await _client.GetAsync("/health/live", timeout.Token);
            return response.IsSuccessStatusCode
                ? null
                : $"The server answered {(int)response.StatusCode} from /health/live.";
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return $"No DotNetCloud server answered at {_client.BaseAddress} ({ex.Message}).";
        }
    }

    /// <inheritdoc/>
    public async Task WaitForStoppedAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + _stopTimeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var state = await ReadModuleAsync(moduleId, cancellationToken);
            if (state is null || string.Equals(state.ProcessStatus, "Stopped", StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        throw new TimeoutException(
            $"Module '{moduleId}' was still {await DescribeAsync(moduleId, cancellationToken)} "
            + $"after {_stopTimeout.TotalSeconds:0}s. The supervisor applies a stop on its reconcile "
            + "interval (ProcessSupervisor:StatusReconcileInterval); raise it or stop the module by hand "
            + $"with 'dotnetcloud module stop {moduleId}'.");
    }

    /// <inheritdoc/>
    public async Task<string?> WaitForHealthyAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + _startTimeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var state = await ReadModuleAsync(moduleId, cancellationToken);

            if (state is not null)
            {
                if (string.Equals(state.Status, "Healthy", StringComparison.Ordinal))
                {
                    return null;
                }

                // A build that failed or exited will not recover by waiting; report it now.
                if (state.ProcessStatus is "Failed" or "Crashed" or "Stopped")
                {
                    return $"Module '{moduleId}' is {state.ProcessStatus}"
                           + (state.Description is { Length: > 0 } ? $": {state.Description}" : ".")
                           + " The server log has its output.";
                }
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        return $"Module '{moduleId}' did not report Healthy within {_startTimeout.TotalSeconds:0}s "
               + $"(last state: {await DescribeAsync(moduleId, cancellationToken)}).";
    }

    private async Task<string> DescribeAsync(string moduleId, CancellationToken cancellationToken)
    {
        var state = await ReadModuleAsync(moduleId, cancellationToken);
        return state is null ? "unknown" : $"{state.ProcessStatus}/{state.Status}";
    }

    private async Task<ModuleReadiness?> ReadModuleAsync(string moduleId, CancellationToken cancellationToken)
    {
        string json;
        try
        {
            using var response = await _client.GetAsync("/health/ready", cancellationToken);

            // 503 is the documented "not ready" answer and still carries the report.
            if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable))
            {
                return null;
            }

            json = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("entries", out var entries)
                || !entries.TryGetProperty(AggregateEntryName, out var aggregate)
                || !aggregate.TryGetProperty("data", out var data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty(moduleId, out var module)
                || module.ValueKind != JsonValueKind.Object)
            {
                // No entry means the supervisor does not know the module (stopped, or never started).
                return null;
            }

            return new ModuleReadiness(
                ReadString(module, "status"),
                ReadString(module, "processStatus"),
                ReadString(module, "description"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private sealed record ModuleReadiness(string? Status, string? ProcessStatus, string? Description);
}
