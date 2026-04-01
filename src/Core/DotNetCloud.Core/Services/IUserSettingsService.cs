using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Provides authenticated user-scoped CRUD operations on settings.
/// </summary>
public interface IUserSettingsService
{
    /// <summary>
    /// Gets a specific user setting by its composite key.
    /// </summary>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <param name="module">The module that owns the setting.</param>
    /// <param name="key">The setting key.</param>
    /// <returns>The user setting DTO, or <see langword="null"/> if not found.</returns>
    Task<UserSettingDto?> GetSettingAsync(Guid userId, string module, string key);

    /// <summary>
    /// Creates or updates a user setting for the authenticated user.
    /// </summary>
    /// <param name="userId">The authenticated user identifier.</param>
    /// <param name="module">The module that owns the setting.</param>
    /// <param name="key">The setting key.</param>
    /// <param name="dto">The setting value and metadata.</param>
    /// <returns>The created or updated user setting DTO.</returns>
    Task<UserSettingDto> UpsertSettingAsync(Guid userId, string module, string key, UpsertUserSettingDto dto);
}
