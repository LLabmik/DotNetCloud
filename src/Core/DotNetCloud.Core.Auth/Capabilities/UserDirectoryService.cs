using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Capabilities;

/// <summary>
/// Implements <see cref="IUserDirectory"/> providing read-only access to user data.
/// </summary>
public sealed class UserDirectoryService : IUserDirectory
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CoreDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="UserDirectoryService"/>.
    /// </summary>
    public UserDirectoryService(UserManager<ApplicationUser> userManager, CoreDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
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

        var results = await _dbContext.Users
            .AsNoTracking()
            .Where(u => idList.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(cancellationToken);

        return results.ToDictionary(u => u.Id, u => u.DisplayName);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(string searchTerm, int maxResults = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return [];

        var term = searchTerm.Trim().ToLower();

        var results = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.IsActive && (u.DisplayName.ToLower().Contains(term) || u.Email!.ToLower().Contains(term)))
            .OrderBy(u => u.DisplayName)
            .Take(maxResults)
            .Select(u => new UserSearchResult(u.Id, u.DisplayName, u.Email!))
            .ToListAsync(cancellationToken);

        return results;
    }
}
