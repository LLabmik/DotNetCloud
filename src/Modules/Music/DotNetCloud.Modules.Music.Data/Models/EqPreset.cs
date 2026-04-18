namespace DotNetCloud.Modules.Music.Models;

/// <summary>
/// Represents an equalizer preset (server stores, client applies).
/// </summary>
public sealed class EqPreset
{
    /// <summary>Unique identifier for this preset.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who owns this preset (null for built-in presets).</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>Preset name (e.g. "Rock", "Jazz", "Flat").</summary>
    public required string Name { get; set; }

    /// <summary>Whether this is a built-in preset (non-deletable).</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>Band gains as JSON: keys are frequency labels, values are gain in dB.</summary>
    public required string BandsJson { get; set; }

    /// <summary>When the preset was created (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the preset was last modified (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
