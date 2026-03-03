using DotNetCloud.Core.Auth.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DotNetCloud.Core.Server.Controllers;

/// <summary>
/// User management endpoints for listing, viewing, updating, deleting, and managing user accounts.
/// </summary>
[ApiController]
[Route("api/v1/core/users")]
[Authorize]
public class UserManagementController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<UserManagementController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserManagementController"/> class.
    /// </summary>
    public UserManagementController(
        IUserManagementService userManagementService,
        ILogger<UserManagementController> logger)
    {
        _userManagementService = userManagementService ?? throw new ArgumentNullException(nameof(userManagementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// List all users with optional pagination and filtering (admin only).
    /// </summary>
    /// <param name="query">Query parameters for pagination, search, and filtering.</param>
    /// <returns>A paginated list of users.</returns>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> ListUsersAsync([FromQuery] UserListQuery query)
    {
        var result = await _userManagementService.ListUsersAsync(query);

        _logger.LogInformation("Listed {Count} users (page {Page}/{TotalPages})",
            result.Items.Count, result.Page, result.TotalPages);

        return Ok(new
        {
            success = true,
            data = result.Items,
            pagination = new
            {
                result.Page,
                result.PageSize,
                result.TotalCount,
                result.TotalPages,
            },
        });
    }

    /// <summary>
    /// Get details for a specific user.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <returns>The user details.</returns>
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetUserAsync(Guid userId)
    {
        // Non-admin users can only view their own profile
        if (!IsAdmin() && !IsCurrentUser(userId))
        {
            return Forbid();
        }

        var user = await _userManagementService.GetUserAsync(userId);
        if (user is null)
        {
            return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found." } });
        }

        return Ok(new { success = true, data = user });
    }

    /// <summary>
    /// Update a user's profile information.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="dto">The update payload.</param>
    /// <returns>The updated user details.</returns>
    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> UpdateUserAsync(Guid userId, [FromBody] UpdateUserDto dto)
    {
        // Non-admin users can only update their own profile and cannot change IsActive
        if (!IsAdmin())
        {
            if (!IsCurrentUser(userId))
            {
                return Forbid();
            }

            // Regular users cannot change their own active status
            dto.IsActive = null;
        }

        try
        {
            var user = await _userManagementService.UpdateUserAsync(userId, dto);
            if (user is null)
            {
                return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found." } });
            }

            _logger.LogInformation("User {UserId} updated", userId);
            return Ok(new { success = true, data = user });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = new { code = "UPDATE_FAILED", message = ex.Message } });
        }
    }

    /// <summary>
    /// Delete a user account (admin only).
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <returns>Confirmation that the user was deleted.</returns>
    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> DeleteUserAsync(Guid userId)
    {
        // Prevent self-deletion
        if (IsCurrentUser(userId))
        {
            return BadRequest(new { success = false, error = new { code = "CANNOT_DELETE_SELF", message = "Cannot delete your own account." } });
        }

        var deleted = await _userManagementService.DeleteUserAsync(userId);
        if (!deleted)
        {
            return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found." } });
        }

        _logger.LogInformation("User {UserId} deleted by admin", userId);
        return Ok(new { success = true, message = "User deleted successfully." });
    }

    /// <summary>
    /// Disable a user account, preventing login (admin only).
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <returns>Confirmation that the user was disabled.</returns>
    [HttpPost("{userId:guid}/disable")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> DisableUserAsync(Guid userId)
    {
        // Prevent self-disable
        if (IsCurrentUser(userId))
        {
            return BadRequest(new { success = false, error = new { code = "CANNOT_DISABLE_SELF", message = "Cannot disable your own account." } });
        }

        var disabled = await _userManagementService.DisableUserAsync(userId);
        if (!disabled)
        {
            return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found." } });
        }

        _logger.LogInformation("User {UserId} disabled by admin", userId);
        return Ok(new { success = true, message = "User disabled successfully." });
    }

    /// <summary>
    /// Enable a previously disabled user account (admin only).
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <returns>Confirmation that the user was enabled.</returns>
    [HttpPost("{userId:guid}/enable")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> EnableUserAsync(Guid userId)
    {
        var enabled = await _userManagementService.EnableUserAsync(userId);
        if (!enabled)
        {
            return NotFound(new { success = false, error = new { code = "USER_NOT_FOUND", message = "User not found." } });
        }

        _logger.LogInformation("User {UserId} enabled by admin", userId);
        return Ok(new { success = true, message = "User enabled successfully." });
    }

    /// <summary>
    /// Reset a user's password (admin only, no current password required).
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="request">The new password request.</param>
    /// <returns>Confirmation that the password was reset.</returns>
    [HttpPost("{userId:guid}/reset-password")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
    public async Task<IActionResult> AdminResetPasswordAsync(Guid userId, [FromBody] AdminResetPasswordRequest request)
    {
        var reset = await _userManagementService.AdminResetPasswordAsync(userId, request);
        if (!reset)
        {
            return BadRequest(new { success = false, error = new { code = "ADMIN_PASSWORD_RESET_FAILED", message = "Password reset failed. User may not exist or password does not meet requirements." } });
        }

        _logger.LogInformation("Admin reset password for user {UserId}", userId);
        return Ok(new { success = true, message = "Password reset successfully." });
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private bool TryGetUserId(out Guid userId)
    {
        var claim = User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out userId);
    }

    private bool IsCurrentUser(Guid userId)
    {
        return TryGetUserId(out var currentUserId) && currentUserId == userId;
    }

    private bool IsAdmin()
    {
        return User.HasClaim("permission", "admin") || User.IsInRole("admin");
    }
}
