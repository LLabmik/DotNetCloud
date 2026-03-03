using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace DotNetCloud.Core.Auth.Capabilities;

/// <summary>
/// Implements <see cref="IUserManager"/> providing privileged user lifecycle operations.
/// </summary>
public sealed class UserManagerService : IUserManager
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CoreDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of <see cref="UserManagerService"/>.
    /// </summary>
    public UserManagerService(UserManager<ApplicationUser> userManager, CoreDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }
}
