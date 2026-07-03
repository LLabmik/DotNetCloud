using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Wraps the native Android <c>AudioEffect.Equalizer</c> to apply server-side EQ presets.
/// Provides a 10-band virtual EQ layer that maps to the device's physical bands via
/// closest-frequency matching, enabling cross-platform preset compatibility with Blazor.
/// </summary>
public interface IEqualizerService
{
    /// <summary>Whether the device EQ is available for the current audio session.</summary>
    bool IsAvailable { get; }

    /// <summary>Number of physical EQ bands on this device (typically 5–6).</summary>
    int NumberOfBands { get; }

    /// <summary>Number of virtual bands (always 10, matching server/Blazor preset format).</summary>
    int VirtualBandCount { get; }

    /// <summary>Gets the center frequencies of each physical band in millihertz.</summary>
    int[] GetBandFrequenciesMhz();

    /// <summary>Gets the 10 virtual band center frequencies in Hz (31, 63, 125, ..., 16K).</summary>
    int[] GetVirtualBandFrequenciesHz();

    /// <summary>Sets the gain for a specific physical band index (gain in millibels).</summary>
    void SetBandLevel(int bandIndex, short gainMb);

    /// <summary>Gets the current gain of each physical device band in millibels.</summary>
    short[] GetBandLevels();

    /// <summary>
    /// Sets the gain for a virtual band (0–9). Maps to the closest physical band
    /// via frequency matching and applies the gain in dB.
    /// </summary>
    void SetVirtualBandGain(int virtualBandIndex, float gainDb);

    /// <summary>
    /// Gets the current gain of all 10 virtual bands in dB, read back from the
    /// mapped physical bands.
    /// </summary>
    float[] GetVirtualBandGainsDb();

    /// <summary>
    /// Bulk-applies an array of 10 virtual band gains (in dB). More efficient than
    /// calling <see cref="SetVirtualBandGain"/> 10 times individually.
    /// </summary>
    void ApplyVirtualBandGains(float[] gainsDb);

    /// <summary>Sets all physical bands from a server preset dictionary (keys: frequency labels, values: dB).</summary>
    void SetAllBands(IDictionary<string, double> bands);

    /// <summary>Applies the band settings from the given preset to physical bands.</summary>
    void ApplyPreset(EqPresetDto preset);

    /// <summary>Resets all bands (physical + virtual) to 0 dB (flat).</summary>
    void Reset();

    /// <summary>Raised when EQ availability changes (e.g. new audio session created).</summary>
    event EventHandler? AvailabilityChanged;
}
