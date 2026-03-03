using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Provides administrative user management operations (list, get, update, delete, disable/enable, password reset).
/// </summary>
public interface IUserManagementService
{
    /// <summary>
    /// Lists users with optional pagination and search.
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination.</param>
    /// <returns>A paginated result of user DTOs.</returns>
    Task<PaginatedResult<UserDto>> ListUsersAsync(UserListQuery query);

    /// <summary>
    /// Gets detailed information for a specific user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <returns>The user DTO, or <see langword="null"/> if not found.</returns>
    Task<UserDto?> GetUserAsync(Guid userId);

    /// <summary>
    /// Updates a user's profile information.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="dto">The update payload.</param>
    /// <returns>The updated user DTO, or <see langword="null"/> if the user was not found.</returns>
    Task<UserDto?> UpdateUserAsync(Guid userId, UpdateUserDto dto);

    /// <summary>
    /// Permanently deletes a user account.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <returns><see langword="true"/> if the user was found and deleted; otherwise <see langword="false"/>.</returns>
    Task<bool> DeleteUserAsync(Guid userId);

    /// <summary>
    /// Disables a user account, preventing login.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <returns><see langword="true"/> if the user was found and disabled; otherwise <see langword="false"/>.</returns>
    Task<bool> DisableUserAsync(Guid userId);

    /// <summary>
    /// Enables a previously disabled user account.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <returns><see langword="true"/> if the user was found and enabled; otherwise <see langword="false"/>.</returns>
    Task<bool> EnableUserAsync(Guid userId);

    /// <summary>
    /// Resets a user's password to a new value (admin operation, no current password required).
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="request">The new password request.</param>
    /// <returns><see langword="true"/> if the password was reset successfully; otherwise <see langword="false"/>.</returns>
    Task<bool> AdminResetPasswordAsync(Guid userId, AdminResetPasswordRequest request);
}
