using DotNetCloud.Core.Data.Entities.Identity;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Auth.Services;

/// <summary>
/// Implements <see cref="IUserManagementService"/> using ASP.NET Core Identity.
/// </summary>
public sealed class UserManagementService : IUserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserManagementService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="UserManagementService"/>.
    /// </summary>
    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        ILogger<UserManagementService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PaginatedResult<UserDto>> ListUsersAsync(UserListQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var usersQuery = _userManager.Users.AsNoTracking();

        // Filter by active status
        if (query.IsActive.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.IsActive == query.IsActive.Value);
        }

        // Search by email or display name
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            usersQuery = usersQuery.Where(u =>
                u.Email!.Contains(search) ||
                u.DisplayName.Contains(search));
        }

        // Sort
        usersQuery = query.SortBy?.ToLowerInvariant() switch
        {
            "displayname" => query.SortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true
                ? usersQuery.OrderByDescending(u => u.DisplayName)
                : usersQuery.OrderBy(u => u.DisplayName),
            "createdat" => query.SortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true
                ? usersQuery.OrderByDescending(u => u.CreatedAt)
                : usersQuery.OrderBy(u => u.CreatedAt),
            "lastloginat" => query.SortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true
                ? usersQuery.OrderByDescending(u => u.LastLoginAt)
                : usersQuery.OrderBy(u => u.LastLoginAt),
            _ => query.SortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true
                ? usersQuery.OrderByDescending(u => u.Email)
                : usersQuery.OrderBy(u => u.Email),
        };

        var totalCount = await usersQuery.CountAsync();

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var users = await usersQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(MapToDto(user, roles));
        }

        return new PaginatedResult<UserDto>
        {
            Items = items.AsReadOnly(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <inheritdoc/>
    public async Task<UserDto?> GetUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return MapToDto(user, roles);
    }

    /// <inheritdoc/>
    public async Task<UserDto?> UpdateUserAsync(Guid userId, UpdateUserDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        if (dto.DisplayName is not null)
        {
            user.DisplayName = dto.DisplayName;
        }

        if (dto.AvatarUrl is not null)
        {
            user.AvatarUrl = dto.AvatarUrl;
        }

        if (dto.Locale is not null)
        {
            user.Locale = dto.Locale;
        }

        if (dto.Timezone is not null)
        {
            user.Timezone = dto.Timezone;
        }

        if (dto.IsActive.HasValue)
        {
            user.IsActive = dto.IsActive.Value;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            _logger.LogWarning("Failed to update user {UserId}: {Errors}", userId, errors);
            throw new InvalidOperationException($"Failed to update user: {errors}");
        }

        _logger.LogInformation("User {UserId} updated", userId);
        var roles = await _userManager.GetRolesAsync(user);
        return MapToDto(user, roles);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            _logger.LogWarning("Failed to delete user {UserId}: {Errors}", userId, errors);
            return false;
        }

        _logger.LogInformation("User {UserId} deleted", userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> DisableUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return false;
        }

        _logger.LogInformation("User {UserId} disabled", userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> EnableUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        user.IsActive = true;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return false;
        }

        _logger.LogInformation("User {UserId} enabled", userId);
        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> AdminResetPasswordAsync(Guid userId, AdminResetPasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            _logger.LogWarning("Admin password reset failed for user {UserId}: {Errors}", userId, errors);
            return false;
        }

        _logger.LogInformation("Admin reset password for user {UserId}", userId);
        return true;
    }

    private static UserDto MapToDto(ApplicationUser user, IList<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email!,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            Locale = user.Locale,
            Timezone = user.Timezone,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            Roles = roles.ToList(),
        };
    }
}
