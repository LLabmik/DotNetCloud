using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Settings;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Auth.Services;

/// <summary>
/// Implements <see cref="IUserSettingsService"/> using EF Core and <see cref="CoreDbContext"/>.
/// </summary>
public sealed class UserSettingsService : IUserSettingsService
{
    private readonly CoreDbContext _dbContext;
    private readonly ILogger<UserSettingsService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="UserSettingsService"/>.
    /// </summary>
    public UserSettingsService(CoreDbContext dbContext, ILogger<UserSettingsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UserSettingDto?> GetSettingAsync(Guid userId, string module, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var setting = await _dbContext.UserSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Module == module && s.Key == key);

        return setting is null ? null : MapToDto(setting);
    }

    /// <inheritdoc/>
    public async Task<UserSettingDto> UpsertSettingAsync(Guid userId, string module, string key, UpsertUserSettingDto dto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(dto);

        var existing = await _dbContext.UserSettings
            .AsTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Module == module && s.Key == key);

        if (existing is not null)
        {
            existing.Value = dto.Value;
            existing.Description = dto.Description;
            existing.IsEncrypted = dto.IsSensitive;
            existing.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Updated user setting {Module}:{Key} for user {UserId}", module, key, userId);
        }
        else
        {
            existing = new UserSetting
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Module = module,
                Key = key,
                Value = dto.Value,
                Description = dto.Description,
                IsEncrypted = dto.IsSensitive,
                UpdatedAt = DateTime.UtcNow,
            };

            _dbContext.UserSettings.Add(existing);
            _logger.LogInformation("Created user setting {Module}:{Key} for user {UserId}", module, key, userId);
        }

        await _dbContext.SaveChangesAsync();
        return MapToDto(existing);
    }

    private static UserSettingDto MapToDto(UserSetting entity)
    {
        return new UserSettingDto
        {
            UserId = entity.UserId,
            Module = entity.Module,
            Key = entity.Key,
            Value = entity.Value,
            Description = entity.Description,
            IsSensitive = entity.IsEncrypted,
        };
    }
}
