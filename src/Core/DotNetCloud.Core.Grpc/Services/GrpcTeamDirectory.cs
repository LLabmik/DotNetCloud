using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Grpc.Services;

/// <summary>
/// gRPC-based implementation of <see cref="ITeamDirectory"/> for use in
/// process-isolated module hosts. Forwards team and membership lookups to
/// Core.Server's CoreCapabilities gRPC service.
/// </summary>
/// <remarks>
/// <para>
/// Connects to Core.Server over the internal gRPC channel identified by the
/// <c>DOTNETCLOUD_CORE_ENDPOINT</c> environment variable (set by
/// <c>ProcessSupervisor</c> when it launches a module host), sending the
/// <c>module-id</c> metadata header so the core can attribute the call.
/// </para>
/// <para>
/// When <c>DOTNETCLOUD_CORE_ENDPOINT</c> is absent (manual host run, unit tests,
/// host started outside the supervisor) the client degrades gracefully: team
/// lookups return empty results / not-found and a warning is logged. Team
/// lookups must never crash module startup or a business operation.
/// </para>
/// <para>
/// Membership enumeration methods (<c>IsTeamMemberAsync</c>, <c>GetTeamMemberAsync</c>,
/// <c>GetTeamMembersAsync</c>) are not yet exposed over gRPC and return conservative
/// defaults (no membership). Module hosts needing them should call Core.Server
/// directly or extend <c>module_capabilities.proto</c>.
/// </para>
/// </remarks>
public sealed class GrpcTeamDirectory : ITeamDirectory, IDisposable
{
    private readonly ILogger<GrpcTeamDirectory> _logger;
    private readonly string? _coreEndpoint;
    private readonly string _moduleId;
    private readonly object _sync = new();
    private GrpcChannel? _channel;
    private CoreCapabilities.CoreCapabilitiesClient? _client;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcTeamDirectory"/> class.
    /// </summary>
    /// <param name="logger">The logger for this client.</param>
    public GrpcTeamDirectory(ILogger<GrpcTeamDirectory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _moduleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? "unknown";

        _coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
        if (string.IsNullOrWhiteSpace(_coreEndpoint))
        {
            _logger.LogWarning(
                "GrpcTeamDirectory: DOTNETCLOUD_CORE_ENDPOINT is not set, so the module cannot reach Core.Server. " +
                "Team membership lookups will return empty results (module not launched by ProcessSupervisor, or " +
                "running in a test host).");
        }
    }

    /// <inheritdoc />
    public async Task<TeamInfo?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            _logger.LogWarning("GetTeam for {TeamId} short-circuited: DOTNETCLOUD_CORE_ENDPOINT is not set.", teamId);
            return null;
        }

        try
        {
            var request = new GetTeamRequest
            {
                TeamId = teamId.ToString(),
            };

            var response = await GetClient().GetTeamAsync(request, GetMetadata(), cancellationToken: cancellationToken);

            _logger.LogInformation("GetTeam gRPC response for {TeamId}: Found={Found}, Team={TeamName}",
                teamId, response.Found, response.Team?.Name);

            if (!response.Found || response.Team is null)
            {
                _logger.LogWarning(
                    "GetTeam: Core.Server reported team {TeamId} as not found. The team is absent from core.teams, " +
                    "soft-deleted, or the lookup failed server-side (check the core log for 'GetTeam').",
                    teamId);
                return null;
            }

            return MapTeam(response.Team);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
            _logger.LogError(
                "GetTeam RPC is Unimplemented on Core.Server — the deployed core binary predates the GetTeam " +
                "capability. Rebuild and redeploy Core.Server from the same branch as this module. TeamId={TeamId}",
                teamId);
            return null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning(
                "Core.Server gRPC service unavailable for GetTeam (StatusCode.Unavailable) — core not reachable " +
                "at DOTNETCLOUD_CORE_ENDPOINT='{Endpoint}'. TeamId={TeamId}",
                Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT"), teamId);
            return null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning("GetTeam gRPC call to Core.Server timed out for {TeamId}", teamId);
            return null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "GetTeam gRPC call to Core.Server failed with status {StatusCode} for {TeamId}: {Detail}",
                ex.StatusCode, teamId, ex.Status.Detail);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling GetTeam for {TeamId}", teamId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TeamInfo>> GetTeamsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            _logger.LogWarning(
                "GetTeamsForUser for {UserId} short-circuited: DOTNETCLOUD_CORE_ENDPOINT is not set, so the module " +
                "cannot reach Core.Server. Team membership will report no teams for this user.",
                userId);
            return [];
        }

        try
        {
            var request = new GetTeamsForUserRequest
            {
                UserId = userId.ToString(),
            };

            var response = await GetClient().GetTeamsForUserAsync(request, GetMetadata(), cancellationToken: cancellationToken);

            _logger.LogDebug("GetTeamsForUser gRPC response for {UserId}: {Count} teams",
                userId, response.Teams.Count);

            return response.Teams.Select(MapTeam).ToList();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
            _logger.LogError(
                "GetTeamsForUser RPC is Unimplemented on Core.Server — the deployed core binary predates the team " +
                "capability. Rebuild and redeploy Core.Server from the same branch as this module. UserId={UserId}",
                userId);
            return [];
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning(
                "Core.Server gRPC service unavailable for GetTeamsForUser (StatusCode.Unavailable) — core not " +
                "reachable at DOTNETCLOUD_CORE_ENDPOINT='{Endpoint}'. UserId={UserId}",
                Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT"), userId);
            return [];
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning("GetTeamsForUser gRPC call to Core.Server timed out for {UserId}", userId);
            return [];
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "GetTeamsForUser gRPC call to Core.Server failed with status {StatusCode} for {UserId}: {Detail}",
                ex.StatusCode, userId, ex.Status.Detail);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling GetTeamsForUser for {UserId}", userId);
            return [];
        }
    }

    /// <inheritdoc />
    public Task<bool> IsTeamMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("IsTeamMemberAsync is not supported via gRPC yet");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<TeamMemberInfo?> GetTeamMemberAsync(Guid teamId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetTeamMemberAsync is not supported via gRPC yet");
        return Task.FromResult<TeamMemberInfo?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TeamMemberInfo>> GetTeamMembersAsync(Guid teamId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetTeamMembersAsync is not supported via gRPC yet");
        return Task.FromResult<IReadOnlyList<TeamMemberInfo>>(Array.Empty<TeamMemberInfo>());
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
                "GrpcTeamDirectory: connected to Core.Server at {Endpoint} (module: {ModuleId})",
                address, _moduleId);

            return _client;
        }
    }

    private Metadata GetMetadata()
    {
        var metadata = new Metadata();
        if (!string.IsNullOrWhiteSpace(_moduleId))
        {
            metadata.Add("module-id", _moduleId);
        }

        return metadata;
    }

    private static TeamInfo MapTeam(TeamInfoMessage message)
    {
        Guid.TryParse(message.Id, out var id);
        Guid.TryParse(message.OrganizationId, out var orgId);
        DateTime.TryParse(message.CreatedAt, out var createdAt);

        return new TeamInfo
        {
            Id = id,
            OrganizationId = orgId,
            Name = message.Name,
            Description = string.IsNullOrEmpty(message.Description) ? null : message.Description,
            MemberCount = message.MemberCount,
            CreatedAt = createdAt == default ? DateTime.UtcNow : createdAt,
        };
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
