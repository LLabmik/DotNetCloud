using CommunityToolkit.Mvvm.ComponentModel;

namespace DotNetCloud.Client.Android.ViewModels;

/// <summary>
/// Bindable model for a single equalizer band, used with the EQ slider UI.
/// Each instance represents one physical band on the device.
/// </summary>
public sealed partial class EqBandModel : ObservableObject
{
    /// <summary>Device band index (0-based).</summary>
    public int BandIndex { get; }

    /// <summary>Human-readable frequency label (e.g. "31", "63", "125", "1K", "4K").</summary>
    public string FrequencyLabel { get; }

    /// <summary>Center frequency in Hz, for reference.</summary>
    public int FrequencyHz { get; }

    /// <summary>
    /// Current gain in dB. Range is approximately -12 to +12.
    /// Two-way bound to the slider. Setting the property triggers
    /// property change notification for both GainDb and ProgressValue.
    /// </summary>
    [ObservableProperty]
    private float _gainDb;

    /// <summary>
    /// Maps the gain (-12..+12 dB) to a 0.0–1.0 range for optional visual display
    /// (e.g. <see cref="Microsoft.Maui.Controls.ProgressBar"/> or color fill).
    /// </summary>
    public float ProgressValue => (GainDb + 12f) / 24f;

    /// <summary>
    /// Initializes a new <see cref="EqBandModel"/>.
    /// </summary>
    /// <param name="bandIndex">Device band index.</param>
    /// <param name="frequencyHz">Center frequency in Hz.</param>
    /// <param name="gainDb">Initial gain in dB (default 0).</param>
    public EqBandModel(int bandIndex, int frequencyHz, float gainDb = 0f)
    {
        BandIndex = bandIndex;
        FrequencyHz = frequencyHz;
        GainDb = gainDb;
        FrequencyLabel = FormatFrequency(frequencyHz);
    }

    /// <summary>
    /// Formats a frequency in Hz into a short human-readable label.
    /// </summary>
    private static string FormatFrequency(int hz)
    {
        if (hz >= 1000)
        {
            var khz = hz / 1000.0;
            return khz % 1 == 0 ? $"{khz:F0}K" : $"{khz:F1}K";
        }
        return hz.ToString();
    }

    partial void OnGainDbChanged(float value)
    {
        // Notify that ProgressValue changed whenever GainDb changes
        OnPropertyChanged(nameof(ProgressValue));
    }
}
