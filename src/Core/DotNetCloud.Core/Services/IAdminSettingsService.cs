using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Core.Services;

/// <summary>
/// Provides administrative CRUD operations on system settings.
/// </summary>
public interface IAdminSettingsService
{
    /// <summary>
    /// Lists all system settings, optionally filtered by module.
    /// </summary>
    /// <param name="module">Optional module filter (e.g., "dotnetcloud.core").</param>
    /// <returns>A read-only list of system setting DTOs.</returns>
    Task<IReadOnlyList<SystemSettingDto>> ListSettingsAsync(string? module = null);

    /// <summary>
    /// Gets a specific system setting by its composite key.
    /// </summary>
    /// <param name="module">The module that owns the setting.</param>
    /// <param name="key">The setting key.</param>
    /// <returns>The setting DTO, or <see langword="null"/> if not found.</returns>
    Task<SystemSettingDto?> GetSettingAsync(string module, string key);

    /// <summary>
    /// Creates or updates a system setting.
    /// </summary>
    /// <param name="module">The module that owns the setting.</param>
    /// <param name="key">The setting key.</param>
    /// <param name="dto">The setting value and metadata.</param>
    /// <returns>The created or updated setting DTO.</returns>
    Task<SystemSettingDto> UpsertSettingAsync(string module, string key, UpsertSystemSettingDto dto);

    /// <summary>
    /// Deletes a system setting.
    /// </summary>
    /// <param name="module">The module that owns the setting.</param>
    /// <param name="key">The setting key.</param>
    /// <returns><see langword="true"/> if the setting was found and deleted; otherwise <see langword="false"/>.</returns>
    Task<bool> DeleteSettingAsync(string module, string key);
}
