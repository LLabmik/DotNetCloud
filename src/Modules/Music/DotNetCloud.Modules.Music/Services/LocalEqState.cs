using DotNetCloud.Core.DTOs;
using System.Text.Json;

namespace DotNetCloud.Modules.Music.Services;

/// <summary>
/// Device-local equalizer state persisted in browser storage (localStorage).
/// Stores the selected preset and the effective 10-band gains so each machine or
/// browser profile can remember its own default EQ independently.
/// </summary>
public sealed record LocalEqState
{
    /// <summary>Number of equalizer bands.</summary>
    public const int BandCount = 10;

    /// <summary>Schema version of the persisted payload.</summary>
    public int Version { get; init; } = 1;

    /// <summary>The preset the bands came from, or <see langword="null"/> for manually adjusted bands.</summary>
    public Guid? PresetId { get; init; }

    /// <summary>
    /// Preset name kept for recovery when the preset ID is unknown on this client
    /// (for example the preset was deleted or re-seeded with a different ID).
    /// </summary>
    public string? PresetName { get; init; }

    /// <summary>Ten band gains in dB (-12 to +12).</summary>
    public required double[] Bands { get; init; }

    /// <summary>Whether this snapshot has the expected number of finite band gains.</summary>
    public bool IsValid => Bands is { Length: BandCount } && Bands.All(double.IsFinite);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Serializes this snapshot for browser storage.</summary>
    /// <returns>The JSON payload.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>
    /// Parses a persisted snapshot.
    /// </summary>
    /// <param name="json">The raw JSON payload read from browser storage.</param>
    /// <returns>
    /// The parsed snapshot, or <see langword="null"/> when the payload is missing, malformed,
    /// or does not contain exactly <see cref="BandCount"/> finite band gains.
    /// </returns>
    public static LocalEqState? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var state = JsonSerializer.Deserialize<LocalEqState>(json, JsonOptions);
            return state is not null && state.IsValid ? state : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves the stored preset against the presets available to the user, falling back to a
    /// case-insensitive name match when the stored ID no longer exists.
    /// </summary>
    /// <param name="presets">The presets currently available to the user.</param>
    /// <returns>The resolved preset ID, or <see langword="null"/> when no match is found.</returns>
    public Guid? ResolvePresetId(IEnumerable<EqPresetDto> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);
        var available = presets as IReadOnlyCollection<EqPresetDto> ?? presets.ToList();

        if (PresetId is { } id && available.Any(p => p.Id == id))
            return id;

        if (!string.IsNullOrWhiteSpace(PresetName))
        {
            var match = available.FirstOrDefault(p =>
                string.Equals(p.Name, PresetName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Id;
        }

        return null;
    }
}
