using Android.Media.Audiofx;
using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// <see cref="IEqualizerService"/> implementation wrapping the native Android
/// <c>AudioEffect.Equalizer</c>. Maps 10-band server presets to device bands
/// and converts dB to millibels.
/// </summary>
internal sealed class AndroidEqualizerService : IEqualizerService, IDisposable
{
    private readonly IMusicPlayerService _player;
    private Equalizer? _equalizer;
    private bool _isAvailable;

    /// <summary>Server preset target frequencies in Hz.</summary>
    private static readonly int[] ServerFrequencies = [31, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    /// <inheritdoc />
    public event EventHandler? AvailabilityChanged;

    /// <summary>Initializes a new <see cref="AndroidEqualizerService"/>.</summary>
    public AndroidEqualizerService(IMusicPlayerService player)
    {
        _player = player;
        _player.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    /// <inheritdoc />
    public bool IsAvailable => _isAvailable && _equalizer is not null;

    /// <inheritdoc />
    public int NumberOfBands => _equalizer?.NumberOfBands ?? 0;

    /// <inheritdoc />
    public int[] GetBandFrequenciesMhz()
    {
        if (_equalizer is null) return [];
        var freqs = new int[_equalizer.NumberOfBands];
        for (int i = 0; i < freqs.Length; i++)
            freqs[i] = _equalizer.GetCenterFreq((short)i);
        return freqs;
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        if (_player.IsPlaying && _player.AudioSessionId != 0)
            CreateEqualizer();
        else
            DisposeEqualizer();
    }

    private void CreateEqualizer()
    {
        try
        {
            DisposeEqualizer();
            _equalizer = new Equalizer(0, _player.AudioSessionId);
            _equalizer.SetEnabled(true);
            _isAvailable = true;
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            _isAvailable = false;
            _equalizer = null;
        }
    }

    private void DisposeEqualizer()
    {
        _equalizer?.SetEnabled(false);
        _equalizer?.Release();
        _equalizer?.Dispose();
        _equalizer = null;
    }

    /// <inheritdoc />
    public void SetBandLevel(int bandIndex, short gainMb)
        => _equalizer?.SetBandLevel((short)bandIndex, gainMb);

    /// <inheritdoc />
    public void SetAllBands(IDictionary<string, double> bands)
    {
        if (_equalizer is null) return;

        var deviceFreqs = new int[_equalizer.NumberOfBands];
        for (int i = 0; i < deviceFreqs.Length; i++)
            deviceFreqs[i] = _equalizer.GetCenterFreq((short)i);

        foreach (var (freqLabel, gainDb) in bands)
        {
            var targetHz = ParseFrequencyLabel(freqLabel);
            var bandIdx = FindClosestBand(deviceFreqs, targetHz);
            if (bandIdx < 0) continue;
            var gainMb = (short)Math.Clamp((int)(gainDb * 100), -1500, 1500);
            _equalizer.SetBandLevel((short)bandIdx, gainMb);
        }
    }

    /// <inheritdoc />
    public void ApplyPreset(EqPresetDto preset) =>
        SetAllBands(new Dictionary<string, double>(preset.Bands));

    /// <inheritdoc />
    public void Reset()
    {
        if (_equalizer is null) return;
        for (short i = 0; i < _equalizer.NumberOfBands; i++)
            _equalizer.SetBandLevel(i, 0);
    }

    private static int ParseFrequencyLabel(string label)
    {
        if (label.EndsWith("K", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(label[..^1], out var kHz))
            return kHz * 1000;
        return int.TryParse(label, out var hz) ? hz : 0;
    }

    private static int FindClosestBand(int[] deviceFreqsMhz, int targetHz)
    {
        int bestIdx = -1, bestDiff = int.MaxValue;
        for (int i = 0; i < deviceFreqsMhz.Length; i++)
        {
            int freqHz = deviceFreqsMhz[i] / 1000;
            int diff = Math.Abs(freqHz - targetHz);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _player.PlaybackStateChanged -= OnPlaybackStateChanged;
        DisposeEqualizer();
    }
}
