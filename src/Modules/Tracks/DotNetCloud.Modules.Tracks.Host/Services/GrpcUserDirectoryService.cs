using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Grpc.Capabilities;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Host.Services;

/// <summary>
/// gRPC-based implementation of <see cref="IUserDirectory"/> for use in
/// process-isolated module hosts. Forwards user directory lookups to
/// Core.Server's CoreCapabilities gRPC service.
/// </summary>
internal sealed class GrpcUserDirectoryService : IUserDirectory, IDisposable
{
    private readonly ILogger<GrpcUserDirectoryService> _logger;
    private readonly GrpcChannel _channel;
    private readonly CoreCapabilities.CoreCapabilitiesClient _client;
    private readonly string _moduleId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrpcUserDirectoryService"/> class.
    /// </summary>
    public GrpcUserDirectoryService(ILogger<GrpcUserDirectoryService> logger)
    {
        _logger = logger;

        var coreEndpoint = Environment.GetEnvironmentVariable("DOTNETCLOUD_CORE_ENDPOINT");
        if (string.IsNullOrWhiteSpace(coreEndpoint))
        {
            _logger.LogWarning("DOTNETCLOUD_CORE_ENDPOINT not set — user directory lookups will return empty results");
            _channel = null!;
            _client = null!;
            _moduleId = "unknown";
            return;
        }

        _moduleId = Environment.GetEnvironmentVariable("DOTNETCLOUD_MODULE_ID") ?? "unknown";
        var address = coreEndpoint.Replace("unix://", "http://").Replace("net.pipe://", "http://");

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
            "GrpcUserDirectoryService: connected to Core.Server at {Endpoint} (module: {ModuleId})",
            address, _moduleId);
    }

    /// <inheritdoc />
    public async Task<Guid?> FindUserIdByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (_client is null || string.IsNullOrWhiteSpace(username))
            return null;

        try
        {
            var metadata = new Metadata { { "module-id", _moduleId } };

            // Search for the exact username — SearchUsers does a case-insensitive
            // substring match, so we filter for exact matches client-side.
            var response = await _client.SearchUsersAsync(new SearchUsersRequest
            {
                Query = username,
                MaxResults = 10,
            }, metadata, cancellationToken: cancellationToken);

            var match = response.Users.FirstOrDefault(
                u => u.DisplayName.Equals(username, StringComparison.OrdinalIgnoreCase));

            if (match is not null && Guid.TryParse(match.Id, out var userId))
                return userId;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FindUserIdByUsernameAsync failed for username '{Username}'", username);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (_client is null)
            return new Dictionary<Guid, string>();

        var results = new Dictionary<Guid, string>();
        foreach (var userId in userIds)
        {
            try
            {
                var metadata = new Metadata { { "module-id", _moduleId } };
                var response = await _client.GetUserAsync(new GetUserRequest
                {
                    UserId = userId.ToString(),
                }, metadata, cancellationToken: cancellationToken);

                if (response.Found && !string.IsNullOrEmpty(response.User?.DisplayName))
                {
                    results[userId] = response.User.DisplayName;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDisplayNamesAsync failed for userId {UserId}", userId);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetAvatarUrlsAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        if (_client is null)
            return new Dictionary<Guid, string>();

        var results = new Dictionary<Guid, string>();
        foreach (var userId in userIds)
        {
            try
            {
                var metadata = new Metadata { { "module-id", _moduleId } };
                var response = await _client.GetUserAsync(new GetUserRequest
                {
                    UserId = userId.ToString(),
                }, metadata, cancellationToken: cancellationToken);

                if (response.Found && !string.IsNullOrEmpty(response.User?.AvatarUrl))
                {
                    results[userId] = response.User.AvatarUrl;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAvatarUrlsAsync failed for userId {UserId}", userId);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(
        string searchTerm, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (_client is null || string.IsNullOrWhiteSpace(searchTerm))
            return [];

        try
        {
            var metadata = new Metadata { { "module-id", _moduleId } };
            var response = await _client.SearchUsersAsync(new SearchUsersRequest
            {
                Query = searchTerm,
                MaxResults = maxResults,
            }, metadata, cancellationToken: cancellationToken);

            return response.Users
                .Where(u => Guid.TryParse(u.Id, out _))
                .Select(u => new UserSearchResult(
                    Guid.Parse(u.Id),
                    u.DisplayName,
                    u.Email))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchUsersAsync failed for query '{Query}'", searchTerm);
            return [];
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
