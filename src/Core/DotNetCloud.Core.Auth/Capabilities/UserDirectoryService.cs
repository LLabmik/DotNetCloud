using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Capabilities;

/// <summary>
/// Implements <see cref="IUserDirectory"/> providing read-only access to user data.
/// </summary>
/// <remarks>
/// Each operation runs on its own short-lived <see cref="CoreDbContext"/> taken from
/// <see cref="IDbContextFactory"/>. The service is scoped while the Blazor circuit holding it is
/// long-lived, so a context captured in the constructor would receive overlapping queries from
/// components whose initializers interleave (which throws "a second operation was started on this
/// context instance"). See <c>UserSettingsService</c> for the same pattern.
/// </remarks>
public sealed class UserDirectoryService : IUserDirectory
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDbContextFactory _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="UserDirectoryService"/>.
    /// </summary>
    /// <param name="userManager">The Identity user manager.</param>
    /// <param name="dbContextFactory">Factory used to create a short-lived context per operation.</param>
    public UserDirectoryService(UserManager<ApplicationUser> userManager, IDbContextFactory dbContextFactory)
    {
        _userManager = userManager;
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public async Task<Guid?> FindUserIdByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        var user = await _userManager.FindByNameAsync(username);
        return user?.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var idList = userIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, string>();

        await using var dbContext = _dbContextFactory.CreateDbContext();

        var results = await dbContext.Users
            .AsNoTracking()
            .Where(u => idList.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(cancellationToken);

        return results.ToDictionary(u => u.Id, u => u.DisplayName);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetAvatarUrlsAsync(
        IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var idList = userIds.Distinct().ToList();
        if (idList.Count == 0)
            return new Dictionary<Guid, string>();

        await using var dbContext = _dbContextFactory.CreateDbContext();

        var results = await dbContext.Users
            .AsNoTracking()
            .Where(u => idList.Contains(u.Id) && u.AvatarUrl != null)
            .Select(u => new { u.Id, u.AvatarUrl })
            .ToListAsync(cancellationToken);

        return results.ToDictionary(u => u.Id, u => u.AvatarUrl!);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(string searchTerm, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return [];

        var term = searchTerm.Trim().ToLower();

        await using var dbContext = _dbContextFactory.CreateDbContext();

        var results = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive && (u.DisplayName.ToLower().Contains(term) || u.Email!.ToLower().Contains(term)))
            .OrderBy(u => u.DisplayName)
            .Take(maxResults)
            .Select(u => new UserSearchResult(u.Id, u.DisplayName, u.Email!))
            .ToListAsync(cancellationToken);

        return results;
    }
}
