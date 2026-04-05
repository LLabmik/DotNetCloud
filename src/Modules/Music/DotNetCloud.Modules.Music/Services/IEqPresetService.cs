using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Manages equalizer presets and user preferences.
/// </summary>
public interface IEqPresetService
{
    /// <summary>Lists all presets (built-in + user-created) for the caller.</summary>
    Task<IReadOnlyList<EqPresetDto>> ListPresetsAsync(CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Gets a preset by ID.</summary>
    Task<EqPresetDto?> GetPresetAsync(Guid presetId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Creates a new custom preset.</summary>
    Task<EqPresetDto> CreatePresetAsync(SaveEqPresetDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing custom preset.</summary>
    Task<EqPresetDto> UpdatePresetAsync(Guid presetId, SaveEqPresetDto dto, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Deletes a custom preset.</summary>
    Task DeletePresetAsync(Guid presetId, CallerContext caller, CancellationToken cancellationToken = default);

    /// <summary>Sets the active equalizer preset for the caller.</summary>
    Task SetActivePresetAsync(Guid? presetId, CallerContext caller, CancellationToken cancellationToken = default);
}
