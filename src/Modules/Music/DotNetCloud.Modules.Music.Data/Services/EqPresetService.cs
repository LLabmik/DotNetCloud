using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Modules.Music.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DotNetCloud.Modules.Music.Data.Services;

/// <summary>
/// Service for managing equalizer presets — CRUD for EQ presets with JSON band data.
/// </summary>
public sealed class EqPresetService
{
    private readonly MusicDbContext _db;
    private readonly ILogger<EqPresetService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EqPresetService"/> class.
    /// </summary>
    public EqPresetService(MusicDbContext db, ILogger<EqPresetService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets all presets available to a user (built-in + user's custom presets).
    /// </summary>
    public async Task<IReadOnlyList<EqPresetDto>> ListPresetsAsync(CallerContext caller, CancellationToken cancellationToken = default)
    {
        var presets = await _db.EqPresets
            .Where(p => p.IsBuiltIn || p.OwnerId == caller.UserId)
            .OrderBy(p => p.IsBuiltIn ? 0 : 1)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return presets.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Gets a preset by ID.
    /// </summary>
    public async Task<EqPresetDto?> GetPresetAsync(Guid presetId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var preset = await _db.EqPresets
            .FirstOrDefaultAsync(p => p.Id == presetId &&
                (p.IsBuiltIn || p.OwnerId == caller.UserId), cancellationToken);

        return preset is null ? null : MapToDto(preset);
    }

    /// <summary>
    /// Creates a custom EQ preset for the user.
    /// </summary>
    public async Task<EqPresetDto> CreatePresetAsync(SaveEqPresetDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var preset = new EqPreset
        {
            OwnerId = caller.UserId,
            Name = dto.Name,
            IsBuiltIn = false,
            BandsJson = JsonSerializer.Serialize(dto.Bands)
        };

        _db.EqPresets.Add(preset);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("EQ preset {PresetId} '{Name}' created by user {UserId}",
            preset.Id, preset.Name, caller.UserId);

        return MapToDto(preset);
    }

    /// <summary>
    /// Updates an existing custom EQ preset.
    /// </summary>
    public async Task<EqPresetDto> UpdatePresetAsync(Guid presetId, SaveEqPresetDto dto, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var preset = await _db.EqPresets
            .FirstOrDefaultAsync(p => p.Id == presetId && p.OwnerId == caller.UserId && !p.IsBuiltIn, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.EqPresetNotFound, "EQ preset not found or is a built-in preset.");

        preset.Name = dto.Name;
        preset.BandsJson = JsonSerializer.Serialize(dto.Bands);
        preset.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return MapToDto(preset);
    }

    /// <summary>
    /// Deletes a custom EQ preset. Built-in presets cannot be deleted.
    /// </summary>
    public async Task DeletePresetAsync(Guid presetId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var preset = await _db.EqPresets
            .FirstOrDefaultAsync(p => p.Id == presetId && p.OwnerId == caller.UserId, cancellationToken)
            ?? throw new BusinessRuleException(ErrorCodes.EqPresetNotFound, "EQ preset not found.");

        if (preset.IsBuiltIn)
            throw new BusinessRuleException(ErrorCodes.MusicAccessDenied, "Built-in presets cannot be deleted.");

        _db.EqPresets.Remove(preset);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("EQ preset {PresetId} deleted by user {UserId}", presetId, caller.UserId);
    }

    /// <summary>
    /// Gets or creates the user's music preferences.
    /// </summary>
    public async Task<UserMusicPreference> GetOrCreateUserPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var prefs = await _db.UserMusicPreferences
            .Include(p => p.ActiveEqPreset)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (prefs is not null) return prefs;

        prefs = new UserMusicPreference { UserId = userId };
        _db.UserMusicPreferences.Add(prefs);
        await _db.SaveChangesAsync(cancellationToken);
        return prefs;
    }

    /// <summary>
    /// Sets the user's active EQ preset.
    /// </summary>
    public async Task SetActivePresetAsync(Guid? presetId, CallerContext caller, CancellationToken cancellationToken = default)
    {
        var prefs = await GetOrCreateUserPreferencesAsync(caller.UserId, cancellationToken);
        prefs.ActiveEqPresetId = presetId;
        prefs.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    internal static EqPresetDto MapToDto(EqPreset preset)
    {
        var bands = new Dictionary<string, double>();
        try
        {
            bands = JsonSerializer.Deserialize<Dictionary<string, double>>(preset.BandsJson)
                ?? new Dictionary<string, double>();
        }
        catch
        {
            // Fallback for malformed JSON
        }

        return new EqPresetDto
        {
            Id = preset.Id,
            Name = preset.Name,
            IsBuiltIn = preset.IsBuiltIn,
            Bands = bands
        };
    }
}
