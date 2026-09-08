using System.Security.Claims;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.UI.Shared.Components.Dialogs;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DotNetCloud.UI.Shared.Services;

/// <summary>
/// Searches share recipients (users platform-wide plus teams the current user
/// belongs to) for the shared <c>DncShareDialog</c>.
/// </summary>
public interface IShareRecipientSearchService
{
    /// <summary>
    /// Searches users and (for the current caller) teams matching the typed term.
    /// </summary>
    /// <param name="term">The typed search text (already trimmed).</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching recipients, users first then teams, ordered by display name.</returns>
    Task<IReadOnlyList<DncShareRecipient>> SearchAsync(
        string term,
        int maxResults = 8,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IShareRecipientSearchService"/>: searches active users via
/// <see cref="IUserDirectory"/> and the current caller's teams via
/// <see cref="ITeamDirectory.GetTeamsForUserAsync"/>.
/// </summary>
public sealed class ShareRecipientSearchService : IShareRecipientSearchService
{
    private readonly IUserDirectory _userDirectory;
    private readonly ITeamDirectory _teamDirectory;
    private readonly AuthenticationStateProvider _authState;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShareRecipientSearchService"/> class.
    /// </summary>
    public ShareRecipientSearchService(
        IUserDirectory userDirectory,
        ITeamDirectory teamDirectory,
        AuthenticationStateProvider authState)
    {
        _userDirectory = userDirectory;
        _teamDirectory = teamDirectory;
        _authState = authState;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DncShareRecipient>> SearchAsync(
        string term,
        int maxResults = 8,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        var trimmed = term.Trim();
        var results = new List<DncShareRecipient>();

        // Users — the directory does a case-insensitive substring match server-side.
        var users = await _userDirectory.SearchUsersAsync(trimmed, maxResults, cancellationToken);
        results.AddRange(users.Select(u => new DncShareRecipient
        {
            Id = u.Id,
            DisplayName = u.DisplayName,
            SecondaryText = string.IsNullOrWhiteSpace(u.Email) ? null : u.Email,
            RecipientType = "User",
        }));

        // Teams the current caller belongs to — filter locally, never exceed the budget.
        var currentUserId = await GetCurrentUserIdAsync();
        if (currentUserId is not null)
        {
            try
            {
                var teams = await _teamDirectory.GetTeamsForUserAsync(currentUserId.Value, cancellationToken);
                var remaining = Math.Max(0, maxResults - results.Count);
                var matches = teams
                    .Where(t => t.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(remaining);

                results.AddRange(matches.Select(t => new DncShareRecipient
                {
                    Id = t.Id,
                    DisplayName = t.Name,
                    SecondaryText = t.MemberCount > 0 ? $"{t.MemberCount} members" : null,
                    RecipientType = "Team",
                }));
            }
            catch (Exception)
            {
                // A team-directory failure must never break recipient search — users still work.
            }
        }

        return results;
    }

    private async Task<Guid?> GetCurrentUserIdAsync()
    {
        try
        {
            var state = await _authState.GetAuthenticationStateAsync();
            var user = state.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var claimValue = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst("sub")?.Value;

            return Guid.TryParse(claimValue, out var userId) ? userId : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// DI registration for the shared share-recipient search service.
/// Call from the host that renders the shared UI (Core.Server) so the
/// <c>DncShareDialog</c> can resolve recipients out of the box.
/// </summary>
public static class ShareRecipientSearchServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IShareRecipientSearchService"/> for scoped resolution.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddShareRecipientSearch(this IServiceCollection services)
    {
        services.TryAddScoped<IShareRecipientSearchService, ShareRecipientSearchService>();
        return services;
    }
}
