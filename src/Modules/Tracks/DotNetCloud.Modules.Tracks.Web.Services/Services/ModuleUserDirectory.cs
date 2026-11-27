using System.Net.Http.Json;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Tracks.Web.Services;

/// <summary>
/// Browser-side <see cref="IUserDirectory"/>: the module UI resolves display names and the @mention
/// typeahead through its own host, which proxies the capability to the core.
/// </summary>
/// <remarks>
/// The host owns the capability grant (the module declares <c>IUserDirectory</c>), so this client
/// never talks to the core itself — it calls two module endpoints:
/// <c>GET api/v1/users/search</c> and <c>POST api/v1/users/display-names</c>.
/// </remarks>
public sealed class ModuleUserDirectory : IUserDirectory
{
    private readonly TracksApi _api;
    private readonly ILogger<ModuleUserDirectory> _logger;

    /// <summary>Initializes a new instance of the <see cref="ModuleUserDirectory"/> class.</summary>
    /// <param name="api">The module API client.</param>
    /// <param name="logger">Logger.</param>
    public ModuleUserDirectory(TracksApi api, ILogger<ModuleUserDirectory> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>No module endpoint resolves a username to an id, so this reports "not found".</remarks>
    public Task<Guid?> FindUserIdByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("FindUserIdByUsernameAsync is not available in the module UI");
        return Task.FromResult<Guid?>(null);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds?.Distinct().ToArray() ?? [];
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        try
        {
            var names = await _api.SendAsync<Dictionary<Guid, string>>(
                HttpMethod.Post,
                "api/v1/users/display-names",
                new { userIds = ids },
                cancellationToken);

            return names ?? new Dictionary<Guid, string>();
        }
        catch (Exception ex) when (ex is TracksApiException or HttpRequestException or NotSupportedException)
        {
            // A display name is cosmetic: a failure must never break the board render.
            _logger.LogWarning(ex, "Could not resolve {Count} display names", ids.Length);
            return new Dictionary<Guid, string>();
        }
    }

    /// <inheritdoc />
    /// <remarks>Avatar URLs are not exposed by the module host; the UI falls back to initials.</remarks>
    public Task<IReadOnlyDictionary<Guid, string>> GetAvatarUrlsAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(
        string searchTerm,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return [];
        }

        try
        {
            return await _api.SendAsync<IReadOnlyList<UserSearchResult>>(
                HttpMethod.Get,
                $"api/v1/users/search?q={Uri.EscapeDataString(searchTerm)}&max={maxResults}",
                cancellationToken: cancellationToken) ?? [];
        }
        catch (Exception ex) when (ex is TracksApiException or HttpRequestException or NotSupportedException)
        {
            _logger.LogWarning(ex, "User search failed");
            return [];
        }
    }
}
