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
/// <remarks>
/// Every operation runs on its own short-lived <see cref="CoreDbContext"/> taken from
/// <see cref="IDbContextFactory"/>, rather than sharing one instance for the lifetime of the DI scope.
/// This service is scoped, but the Blazor circuit that resolves it is long-lived and several components
/// interleave their initializers on it — <c>MainLayout</c> restores the collapsed-sidebar preference
/// while <c>Home</c> loads the widget layout, and the module pages read their own view settings. A shared
/// context then receives overlapping queries and EF throws <c>InvalidOperationException</c> ("A second
/// operation was started on this context instance"), which surfaced as "Failed to load home widget
/// preferences" and silently fell back to the default widget layout. The transient <see cref="CoreDbContext"/>
/// registration cannot prevent that: a scoped wrapper captures one instance for the whole scope.
/// </remarks>
public sealed class UserSettingsService : IUserSettingsService
{
    private readonly IDbContextFactory _dbContextFactory;
    private readonly ILogger<UserSettingsService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="UserSettingsService"/>.
    /// </summary>
    /// <param name="dbContextFactory">Factory used to create a short-lived context per operation.</param>
    /// <param name="logger">The logger.</param>
    public UserSettingsService(IDbContextFactory dbContextFactory, ILogger<UserSettingsService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UserSettingDto?> GetSettingAsync(Guid userId, string module, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await using var dbContext = _dbContextFactory.CreateDbContext();

        var setting = await dbContext.UserSettings
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

        await using var dbContext = _dbContextFactory.CreateDbContext();

        var existing = await dbContext.UserSettings
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
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Module = module,
                Key = key,
                Value = dto.Value,
                Description = dto.Description,
                IsEncrypted = dto.IsSensitive,
                UpdatedAt = DateTime.UtcNow,
            };

            dbContext.UserSettings.Add(existing);
            _logger.LogInformation("Created user setting {Module}:{Key} for user {UserId}", module, key, userId);
        }

        await dbContext.SaveChangesAsync();
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
