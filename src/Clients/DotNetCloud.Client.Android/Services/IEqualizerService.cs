using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Wraps the native Android <c>AudioEffect.Equalizer</c> to apply server-side EQ presets.
/// </summary>
public interface IEqualizerService
{
    /// <summary>Whether the device EQ is available for the current audio session.</summary>
    bool IsAvailable { get; }

    /// <summary>Number of EQ bands on this device.</summary>
    int NumberOfBands { get; }

    /// <summary>Gets the center frequencies of each band in millihertz.</summary>
    int[] GetBandFrequenciesMhz();

    /// <summary>Sets the gain for a specific band index (gain in millibels).</summary>
    void SetBandLevel(int bandIndex, short gainMb);

    /// <summary>Gets the current gain of each device band in millibels.</summary>
    short[] GetBandLevels();

    /// <summary>Sets all bands from a server preset dictionary (keys: frequency labels, values: dB).</summary>
    void SetAllBands(IDictionary<string, double> bands);

    /// <summary>Applies the band settings from the given preset.</summary>
    void ApplyPreset(EqPresetDto preset);

    /// <summary>Resets all bands to 0 dB (flat).</summary>
    void Reset();

    /// <summary>Raised when EQ availability changes (e.g. new audio session created).</summary>
    event EventHandler? AvailabilityChanged;
}
