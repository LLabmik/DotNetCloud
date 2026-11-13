using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Entities.Modules;
using DotNetCloud.Core.Modules;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Services;

/// <summary>
/// EF Core-backed implementation of <see cref="IModuleSettingsStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// The module id is supplied by the caller and must already be the authenticated module identity —
/// the store performs no authorization of its own. Keys and values are validated here as well as at
/// the RPC boundary so no path into the store can persist an oversized or malformed entry.
/// </para>
/// <para>
/// Values written with <c>isSecret</c> are protected by <see cref="IModuleSettingCipher"/> before
/// they reach the database, and are never returned by <see cref="ListAsync"/>. When no cipher is
/// registered a secret write fails closed rather than silently storing plain text.
/// </para>
/// </remarks>
public class ModuleSettingsStore : IModuleSettingsStore
{
    private readonly CoreDbContext _dbContext;
    private readonly IModuleSettingCipher? _cipher;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModuleSettingsStore"/> class.
    /// </summary>
    /// <param name="dbContext">The core database context.</param>
    /// <param name="cipher">
    /// Protects secret values at rest. Optional so hosts that only ever read non-secret settings
    /// (tests, the CLI) can construct the store without a key ring; a secret write then fails.
    /// </param>
    public ModuleSettingsStore(CoreDbContext dbContext, IModuleSettingCipher? cipher = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cipher = cipher;
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string moduleId, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var setting = await _dbContext.ModuleSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ModuleId == moduleId && s.Key == key, cancellationToken);

        if (setting is null)
        {
            return null;
        }

        return setting.IsSecret ? RequireCipher().Unprotect(moduleId, setting.Value) : setting.Value;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModuleSettingEntry>> ListAsync(
        string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var rows = await _dbContext.ModuleSettings
            .AsNoTracking()
            .Where(s => s.ModuleId == moduleId)
            .OrderBy(s => s.Key)
            .ToListAsync(cancellationToken);

        return rows
            .Select(setting => new ModuleSettingEntry(
                setting.ModuleId,
                setting.Key,
                setting.IsSecret,
                HasValue: true,
                // A secret is never handed back to a listing, not even to an administrator.
                Value: setting.IsSecret ? null : setting.Value,
                setting.UpdatedAtUtc,
                setting.UpdatedByUserId))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        string moduleId, string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var existing = await _dbContext.ModuleSettings
            .AsTracking()
            .FirstOrDefaultAsync(s => s.ModuleId == moduleId && s.Key == key, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        _dbContext.ModuleSettings.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
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

        // Protect before the value reaches the database; a secret must never be stored as plain text.
        var stored = isSecret ? RequireCipher().Protect(moduleId, value) : value;

        // AsTracking is required: CoreDbContext is NoTracking by default, so a detached row would be
        // mutated and silently discarded by SaveChanges.
        var existing = await _dbContext.ModuleSettings
            .AsTracking()
            .FirstOrDefaultAsync(s => s.ModuleId == moduleId && s.Key == key, cancellationToken);

        if (existing is null)
        {
            _dbContext.ModuleSettings.Add(new ModuleSetting
            {
                ModuleId = moduleId,
                Key = key,
                Value = stored,
                IsSecret = isSecret,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByUserId = updatedByUserId,
            });
        }
        else
        {
            existing.Value = stored;
            existing.IsSecret = isSecret;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = updatedByUserId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private IModuleSettingCipher RequireCipher() =>
        _cipher ?? throw new InvalidOperationException(
            "No IModuleSettingCipher is registered, so a secret module setting cannot be stored safely.");
}
