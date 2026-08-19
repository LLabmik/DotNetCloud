using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Grpc.Clients;

/// <summary>
/// gRPC client for the <c>CoreCapabilities.LogAudit</c> rpc hosted by Core.Server.
/// </summary>
/// <remarks>
/// <para>
/// Process-isolated module hosts use this client to persist audit trail entries
/// (SOC 2 CC4). It connects to Core.Server over the internal gRPC channel the same
/// way <c>TokenIntrospectionClient</c> does — the <c>DOTNETCLOUD_CORE_ENDPOINT</c>
/// environment variable is set by <c>ProcessSupervisor</c> when it launches the module.
/// </para>
/// <para>
/// When <c>DOTNETCLOUD_CORE_ENDPOINT</c> is absent (manual host run, unit/integration
/// tests, host started outside the supervisor) the client degrades to a no-op that
/// logs a warning on every call. Auditing must never crash module startup or a
/// business operation.
/// </para>
/// </remarks>
internal sealed class AuditLoggerGrpcClient : IAuditLogger, IDisposable
{
    private readonly ILogger<AuditLoggerGrpcClient> _logger;
    private readonly string? _coreEndpoint;
    private GrpcChannel? _channel;
    private CoreCapabilities.CoreCapabilitiesClient? _client;
    private readonly object _sync = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditLoggerGrpcClient"/> class.
    /// </summary>
    /// <param name="logger">The logger for this client.</param>
    public AuditLoggerGrpcClient(ILogger<AuditLoggerGrpcClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
        if (string.IsNullOrWhiteSpace(_coreEndpoint))
        {
            _logger.LogWarning(
                "AuditLoggerGrpcClient: DOTNETCLOUD_CORE_ENDPOINT is not set. Audit logging is disabled " +
                "(module not launched by ProcessSupervisor, or running in a test host).");
        }
    }

    private CoreCapabilities.CoreCapabilitiesClient GetClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_client is not null)
            return _client;

        lock (_sync)
        {
            if (_client is not null)
                return _client;

            // Convert from the internal URL format used by ProcessSupervisor.
            var address = _coreEndpoint!
                .Replace("unix://", "http://")
                .Replace("net.pipe://", "http://");

            _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    UseProxy = false,
                    AllowAutoRedirect = false,
                    UseCookies = false,
                },
                ThrowOperationCanceledOnCancellation = true,
            });

            _client = new CoreCapabilities.CoreCapabilitiesClient(_channel);

            _logger.LogInformation(
                "AuditLoggerGrpcClient: connected to Core.Server at {Endpoint}",
                address);

            return _client;
        }
    }

    /// <inheritdoc />
    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.Caller);

        if (string.IsNullOrWhiteSpace(_coreEndpoint))
        {
            // No Core.Server endpoint (test host / manual run) — do not throw, but
            // surface the gap so operators don't mistake silence for auditing.
            _logger.LogWarning(
                "AuditLoggerGrpcClient: audit entry NOT persisted for {AuditAction} on {EntityType}/{EntityId} " +
                "in {ModuleId} (DOTNETCLOUD_CORE_ENDPOINT not set).",
                entry.Action, entry.EntityType, entry.EntityId, entry.ModuleId);
            return;
        }

        try
        {
            var request = new LogAuditRequest
            {
                ModuleId = entry.ModuleId,
                Action = (int)entry.Action,
                EntityType = entry.EntityType,
                EntityId = entry.EntityId.ToString(),
                Description = entry.Description ?? string.Empty,
            };
            request.Caller = new CallerContextMessage
            {
                UserId = entry.Caller.UserId.ToString(),
                CallerType = entry.Caller.Type.ToString(),
                ModuleId = entry.ModuleId,
            };
            foreach (var role in entry.Caller.Roles)
                request.Caller.Roles.Add(role);

            var response = await GetClient().LogAuditAsync(request, cancellationToken: cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning(
                    "LogAudit: Core.Server reported failure for {AuditAction} on {EntityType}/{EntityId}",
                    entry.Action, entry.EntityType, entry.EntityId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "LogAudit: gRPC call failed for {AuditAction} on {EntityType}/{EntityId} in {ModuleId}",
                entry.Action, entry.EntityType, entry.EntityId, entry.ModuleId);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _channel?.Dispose();
    }
}
