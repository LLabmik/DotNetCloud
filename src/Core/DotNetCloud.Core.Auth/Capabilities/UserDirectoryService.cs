using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.AspNetCore.Identity;

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
}
