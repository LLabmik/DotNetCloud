using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// gRPC-based implementation of <see cref="IGroupDirectory"/> that calls the
/// Core.Server's CoreCapabilities service.
/// </summary>
internal sealed class GrpcGroupDirectory : IGroupDirectory, IDisposable
{
    private readonly ILogger<GrpcGroupDirectory> _logger;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<CoreCapabilities.CoreCapabilitiesClient> _client;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcGroupDirectory"/> class.
    /// </summary>
    public GrpcGroupDirectory(ILogger<GrpcGroupDirectory> logger)
    {
        _logger = logger;
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<CoreCapabilities.CoreCapabilitiesClient>(
            () => new CoreCapabilities.CoreCapabilitiesClient(_channel.Value));
    }

    /// <inheritdoc />
    public async Task<GroupInfo?> GetGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning(
                "GetGroup for {GroupId} short-circuited: DOTNETCLOUD_CORE_ENDPOINT is not set, so the module " +
                "cannot reach Core.Server. Group validation will report this group as 'not found'.",
                groupId);
            return null;
        }

        try
        {
            var request = new GetGroupRequest
            {
                GroupId = groupId.ToString(),
            };

            var callOptions = new CallOptions(cancellationToken: cancellationToken);
            var response = await _client.Value.GetGroupAsync(request, callOptions);

            _logger.LogInformation("GetGroup gRPC response for {GroupId}: Found={Found}, Group={GroupName}",
                groupId, response.Found, response.Group?.Name);

            if (!response.Found || response.Group is null)
            {
                _logger.LogWarning(
                    "GetGroup: Core.Server reported group {GroupId} as not found. The group is absent from " +
                    "core.groups, soft-deleted, or the lookup failed server-side (check the core log for 'GetGroup').",
                    groupId);
                return null;
            }

            return MapGroup(response.Group);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
        {
            _logger.LogError(
                "GetGroup RPC is Unimplemented on Core.Server — the deployed core binary predates the GetGroup " +
                "capability. Rebuild and redeploy Core.Server from the same branch as this module. GroupId={GroupId}",
                groupId);
            return null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
        {
            _logger.LogWarning(
                "Core.Server gRPC service unavailable for GetGroup (StatusCode.Unavailable) — core not reachable " +
                "at DOTNETCLOUD_CORE_ENDPOINT='{Endpoint}'. GroupId={GroupId}",
                Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT"), groupId);
            return null;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
        {
            _logger.LogWarning("GetGroup gRPC call to Core.Server timed out for {GroupId}", groupId);
            return null;
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "GetGroup gRPC call to Core.Server failed with status {StatusCode} for {GroupId}: {Detail}",
                ex.StatusCode, groupId, ex.Status.Detail);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling GetGroup for {GroupId}", groupId);
            return null;
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GroupInfo>> GetGroupsForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetGroupsForOrganizationAsync is not supported via gRPC yet");
        return Task.FromResult<IReadOnlyList<GroupInfo>>(Array.Empty<GroupInfo>());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GroupInfo>> GetGroupsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetGroupsForUserAsync is not supported via gRPC yet");
        return Task.FromResult<IReadOnlyList<GroupInfo>>(Array.Empty<GroupInfo>());
    }

    /// <inheritdoc />
    public Task<bool> IsGroupMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("IsGroupMemberAsync is not supported via gRPC yet");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<GroupMemberInfo?> GetGroupMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetGroupMemberAsync is not supported via gRPC yet");
        return Task.FromResult<GroupMemberInfo?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GroupMemberInfo>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("GetGroupMembersAsync is not supported via gRPC yet");
        return Task.FromResult<IReadOnlyList<GroupMemberInfo>>(Array.Empty<GroupMemberInfo>());
    }

    /// <summary>
    /// Gets whether the core server gRPC endpoint is configured.
    /// </summary>
    private static bool IsAvailable
    {
        get
        {
            var endpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
            return !string.IsNullOrWhiteSpace(endpoint);
        }
    }

    private static GrpcChannel CreateChannel()
    {
        var endpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");

        var address = endpoint;
        if (!string.IsNullOrWhiteSpace(address) &&
            address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            address = "http://" + address["https://".Length..];
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            address = "http://localhost:0";
        }

        return GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials = true,
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                ConnectTimeout = TimeSpan.FromSeconds(5),
            }
        });
    }

    private static GroupInfo MapGroup(GroupInfoMessage message)
    {
        Guid.TryParse(message.Id, out var id);
        Guid.TryParse(message.OrganizationId, out var orgId);
        DateTime.TryParse(message.CreatedAt, out var createdAt);

        return new GroupInfo
        {
            Id = id,
            OrganizationId = orgId,
            Name = message.Name,
            Description = string.IsNullOrEmpty(message.Description) ? null : message.Description,
            IsAllUsersGroup = message.IsAllUsersGroup,
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

        if (_channel.IsValueCreated)
        {
            _channel.Value.Dispose();
        }
    }
}
