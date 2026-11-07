using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Services;

/// <summary>
/// EF Core-backed implementation of <see cref="IModuleSettingsStore"/>.
/// </summary>
/// <remarks>
/// The module id is supplied by the caller and must already be the authenticated module identity —
/// the store performs no authorization of its own. Keys and values are validated here as well as at
/// the RPC boundary so no path into the store can persist an oversized or malformed entry.
/// </remarks>
public class ModuleSettingsStore : IModuleSettingsStore
{
    private readonly CoreDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleSettingsStore"/> class.
    /// </summary>
    /// <param name="dbContext">The core database context.</param>
    public ModuleSettingsStore(CoreDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string moduleId, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var setting = await _dbContext.ModuleSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ModuleId == moduleId && s.Key == key, cancellationToken);

        return setting?.Value;
    }

    /// <inheritdoc />
    public async Task SetAsync(
        string moduleId,
        string key,
        string value,
        bool isSecret = false,
        Guid? updatedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (!ModuleSettingLimits.IsValidKey(key))
        {
            throw new ArgumentException($"Invalid module setting key '{key}'.", nameof(key));
        }

        if (!ModuleSettingLimits.IsValidValue(value))
        {
            throw new ArgumentException(
                $"Module setting value exceeds {ModuleSettingLimits.MaxValueLength} characters.",
                nameof(value));
        }

        var existing = await _dbContext.ModuleSettings
            .FirstOrDefaultAsync(s => s.ModuleId == moduleId && s.Key == key, cancellationToken);

        if (existing is null)
        {
            _dbContext.ModuleSettings.Add(new ModuleSetting
            {
                ModuleId = moduleId,
                Key = key,
                Value = value,
                IsSecret = isSecret,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByUserId = updatedByUserId,
            });
        }
        else
        {
            existing.Value = value;
            existing.IsSecret = isSecret;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = updatedByUserId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
