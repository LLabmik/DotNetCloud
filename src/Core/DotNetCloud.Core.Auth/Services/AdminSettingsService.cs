using DotNetCloud.Core.Constants;
using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Auth.Services;

/// <summary>
/// Implements <see cref="IAdminSettingsService"/> using EF Core and <see cref="CoreDbContext"/>.
/// </summary>
public sealed class AdminSettingsService : IAdminSettingsService
{
    private readonly CoreDbContext _dbContext;
    private readonly ILogger<AdminSettingsService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AdminSettingsService"/>.
    /// </summary>
    public AdminSettingsService(CoreDbContext dbContext, ILogger<AdminSettingsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SystemSettingDto>> ListSettingsAsync(string? module = null)
    {
        var query = _dbContext.SystemSettings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(module))
        {
            query = query.Where(s => s.Module.Contains(module));
        }

        var settings = await query
            .OrderBy(s => s.Module)
            .ThenBy(s => s.Key)
            .Select(s => MapToDto(s))
            .ToListAsync();

        return settings.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task<SystemSettingDto?> GetSettingAsync(string module, string key)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(key);

        var setting = await _dbContext.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Module == module && s.Key == key);

        return setting is null ? null : MapToDto(setting);
    }

    /// <inheritdoc/>
    public async Task<SystemSettingDto> UpsertSettingAsync(string module, string key, UpsertSystemSettingDto dto)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(dto);

        // Mutual exclusion validation: Demo Mode and Closed System cannot both be enabled
        await ValidateMutualExclusionAsync(module, key, dto.Value);

        var existing = await _dbContext.SystemSettings
            .AsTracking()
            .FirstOrDefaultAsync(s => s.Module == module && s.Key == key);

        if (existing is not null)
        {
            existing.Value = dto.Value;
            existing.Description = dto.Description;
            existing.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Updated system setting {Module}:{Key}", module, key);
        }
        else
        {
            existing = new SystemSetting
            {
                Module = module,
                Key = key,
                Value = dto.Value,
                Description = dto.Description,
                UpdatedAt = DateTime.UtcNow,
            };

            _dbContext.SystemSettings.Add(existing);
            _logger.LogInformation("Created system setting {Module}:{Key}", module, key);
        }

        await _dbContext.SaveChangesAsync();
        return MapToDto(existing);
    }

    /// <summary>
    /// Validates that Demo Mode and Closed System mode are not both enabled simultaneously.
    /// </summary>
    private async Task ValidateMutualExclusionAsync(string module, string key, string newValue)
    {
        // Only validate core module settings
        if (module != SystemSettingKeys.CoreModule)
            return;

        // Only validate the boolean "true" state
        if (newValue != "true")
            return;

        if (key == SystemSettingKeys.DemoModeEnabled)
        {
            // Check if ClosedSystemEnabled is already "true"
            var closedSetting = await _dbContext.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Module == SystemSettingKeys.CoreModule &&
                    s.Key == SystemSettingKeys.ClosedSystemEnabled);

            if (closedSetting?.Value == "true")
            {
                throw new InvalidOperationException(
                    "Cannot enable Demo Mode while Closed System mode is active. " +
                    "Disable Closed System mode first.");
            }
        }
        else if (key == SystemSettingKeys.ClosedSystemEnabled)
        {
            // Check if DemoModeEnabled is already "true"
            var demoSetting = await _dbContext.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.Module == SystemSettingKeys.CoreModule &&
                    s.Key == SystemSettingKeys.DemoModeEnabled);

            if (demoSetting?.Value == "true")
            {
                throw new InvalidOperationException(
                    "Cannot enable Closed System mode while Demo Mode is active. " +
                    "Disable Demo Mode first.");
            }
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteSettingAsync(string module, string key)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(key);

        var setting = await _dbContext.SystemSettings
            .AsTracking()
            .FirstOrDefaultAsync(s => s.Module == module && s.Key == key);

        if (setting is null)
        {
            return false;
        }

        _dbContext.SystemSettings.Remove(setting);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted system setting {Module}:{Key}", module, key);
        return true;
    }

    private static SystemSettingDto MapToDto(SystemSetting entity)
    {
        return new SystemSettingDto
        {
            Module = entity.Module,
            Key = entity.Key,
            Value = entity.Value,
            Description = entity.Description,
        };
    }
}
