using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data.Services;

/// <summary>
/// gRPC-based implementation of <see cref="IUserDirectory"/> that calls Core.Server's
/// CoreCapabilities service.
/// </summary>
/// <remarks>
/// <para>
/// Used by process-isolated module hosts (the Files host, and the media hosts that share
/// <c>AddFilesServices</c>) to resolve user display names and avatars. The primary consumer is
/// the WOPI <c>CheckFileInfo</c> response, where Collabora renders the editor's own
/// "UserFriendlyName" — without this capability the editor falls back to showing the raw user GUID.
/// </para>
/// <para>
/// Mirrors <see cref="GrpcGroupDirectory"/>: connects via the <c>DOTNETCLOUD_CORE_ENDPOINT</c>
/// environment variable (set by ProcessSupervisor) and degrades gracefully — lookups return empty
/// results when the endpoint is absent or Core.Server is unreachable, so a directory lookup can
/// never fail a user-facing operation.
/// </para>
/// </remarks>
internal sealed class GrpcUserDirectory : IUserDirectory, IDisposable
{
    private readonly ILogger<GrpcUserDirectory> _logger;
    private readonly string _moduleId;
    private readonly CoreCapabilities.CoreCapabilitiesClient? _injectedClient;
    private readonly Lazy<GrpcChannel> _channel;
    private readonly Lazy<CoreCapabilities.CoreCapabilitiesClient> _client;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcUserDirectory"/> class.
    /// </summary>
    public GrpcUserDirectory(ILogger<GrpcUserDirectory> logger)
        : this(logger, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcUserDirectory"/> class with an explicit
    /// capability client. Used by tests; production resolves the client from
    /// <c>DOTNETCLOUD_CORE_ENDPOINT</c>.
    /// </summary>
    internal GrpcUserDirectory(ILogger<GrpcUserDirectory> logger, CoreCapabilities.CoreCapabilitiesClient? client)
    {
        _logger = logger;
        _injectedClient = client;
        _moduleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? "unknown";
        _channel = new Lazy<GrpcChannel>(CreateChannel);
        _client = new Lazy<CoreCapabilities.CoreCapabilitiesClient>(
            () => client ?? new CoreCapabilities.CoreCapabilitiesClient(_channel.Value));
    }

    /// <inheritdoc />
    public async Task<Guid?> FindUserIdByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        // SearchUsers does a case-insensitive substring match, so filter for an exact match here.
        var matches = await SearchCoreAsync(username, 10, cancellationToken);
        var match = matches.FirstOrDefault(u => string.Equals(u.DisplayName, username, StringComparison.OrdinalIgnoreCase));
        return match?.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var results = new Dictionary<Guid, string>();
        foreach (var userId in userIds.Distinct())
        {
            var user = await GetUserCoreAsync(userId, cancellationToken);
            if (user is not null && !string.IsNullOrWhiteSpace(user.DisplayName))
                results[userId] = user.DisplayName;
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetAvatarUrlsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var results = new Dictionary<Guid, string>();
        foreach (var userId in userIds.Distinct())
        {
            var user = await GetUserCoreAsync(userId, cancellationToken);
            if (user is not null && !string.IsNullOrWhiteSpace(user.AvatarUrl))
                results[userId] = user.AvatarUrl;
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(
        string searchTerm,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return [];

        return await SearchCoreAsync(searchTerm, maxResults, cancellationToken);
    }

    /// <summary>
    /// Calls Core.Server's <c>GetUser</c> capability and returns the mapped user, or
    /// <see langword="null"/> when the user is unknown or the call fails.
    /// </summary>
    private async Task<UserInfo?> GetUserCoreAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning(
                "GetUser for {UserId} short-circuited: DOTNETCLOUD_CORE_ENDPOINT is not set, so the module " +
                "cannot reach Core.Server. The display name will fall back to the user id.",
                userId);
            return null;
        }

        try
        {
            var request = new GetUserRequest { UserId = userId.ToString() };
            var response = await _client.Value.GetUserAsync(
                request, ModuleIdMetadata(), cancellationToken: cancellationToken);

            if (!response.Found || response.User is null)
            {
                _logger.LogDebug("GetUser: Core.Server reported user {UserId} as not found", userId);
                return null;
            }

            return response.User;
        }
        catch (RpcException ex)
        {
            LogRpcFailure(ex, "GetUser", userId.ToString());
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling GetUser for {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Calls Core.Server's <c>SearchUsers</c> capability and maps the results.
    /// </summary>
    private async Task<List<UserSearchResult>> SearchCoreAsync(
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning(
                "SearchUsers('{Query}') short-circuited: DOTNETCLOUD_CORE_ENDPOINT is not set, so the module " +
                "cannot reach Core.Server.",
                query);
            return [];
        }

        try
        {
            var request = new SearchUsersRequest
            {
                Query = query,
                MaxResults = maxResults > 0 ? maxResults : 20,
            };

            var response = await _client.Value.SearchUsersAsync(
                request, ModuleIdMetadata(), cancellationToken: cancellationToken);

            return response.Users
                .Select(u => Guid.TryParse(u.Id, out var id)
                    ? new UserSearchResult(id, u.DisplayName, u.Email)
                    : null)
                .Where(r => r is not null)
                .Select(r => r!)
                .ToList();
        }
        catch (RpcException ex)
        {
            LogRpcFailure(ex, "SearchUsers", query);
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling SearchUsers for query '{Query}'", query);
            return [];
        }
    }

    private Metadata ModuleIdMetadata() => new() { { "module-id", _moduleId } };

    /// <summary>
    /// Logs an RPC failure with a message that identifies whether the core is unreachable or
    /// simply predates the capability, so the operator knows which way to fix it.
    /// </summary>
    private void LogRpcFailure(RpcException ex, string operation, string argument)
    {
        switch (ex.StatusCode)
        {
            case StatusCode.Unimplemented:
                _logger.LogError(
                    "{Operation} RPC is Unimplemented on Core.Server — the deployed core binary predates the " +
                    "{Operation} capability. Rebuild and redeploy Core.Server. Argument={Argument}",
                    operation, operation, argument);
                break;
            case StatusCode.Unavailable:
                _logger.LogWarning(
                    "Core.Server gRPC service unavailable for {Operation} — core not reachable at " +
                    "DOTNETCLOUD_CORE_ENDPOINT='{Endpoint}'. Argument={Argument}",
                    operation, Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT"), argument);
                break;
            case StatusCode.DeadlineExceeded:
                _logger.LogWarning("{Operation} gRPC call to Core.Server timed out. Argument={Argument}", operation, argument);
                break;
            default:
                _logger.LogError(ex,
                    "{Operation} gRPC call to Core.Server failed with status {StatusCode}: {Detail}. Argument={Argument}",
                    operation, ex.StatusCode, ex.Status.Detail, argument);
                break;
        }
    }

    /// <summary>
    /// Gets whether the core server gRPC endpoint is configured (or a client was injected).
    /// </summary>
    private bool IsAvailable =>
        _injectedClient is not null
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT"));

    private static GrpcChannel CreateChannel()
    {
        var address = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");

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

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_channel.IsValueCreated)
            _channel.Value.Dispose();
    }
}
