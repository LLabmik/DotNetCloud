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
    private int _lastAudioSessionId;

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
        if (_equalizer is null)
            return [];
        var freqs = new int[_equalizer.NumberOfBands];
        for (int i = 0; i < freqs.Length; i++)
            freqs[i] = _equalizer.GetCenterFreq((short)i);
        return freqs;
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[EQ-SVC] OnPlaybackStateChanged: IsPlaying={_player.IsPlaying}, SessionId={_player.AudioSessionId}, _isAvailable={_isAvailable}, _lastSessionId={_lastAudioSessionId}");
        if (_player.IsPlaying && _player.AudioSessionId != 0)
        {
            // Only create/recreate the EQ if it's not yet available or the audio session changed
            // (new track). Avoids destroying/recreating the EQ on every minor state change,
            // which would reset all band gains to zero.
            if (!_isAvailable || _player.AudioSessionId != _lastAudioSessionId)
            {
                System.Diagnostics.Debug.WriteLine($"[EQ-SVC] Creating equalizer (wasAvailable={_isAvailable}, prevSession={_lastAudioSessionId}, newSession={_player.AudioSessionId})");
                _lastAudioSessionId = _player.AudioSessionId;
                CreateEqualizer();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[EQ-SVC] Skipping equalizer recreation (already available, same session)");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[EQ-SVC] Disposing equalizer");
            DisposeEqualizer();
        }
    }

    private void CreateEqualizer()
    {
        try
        {
            DisposeEqualizer();
            _equalizer = new Equalizer(0, _player.AudioSessionId);
            _equalizer.SetEnabled(true);
            _isAvailable = true;
            System.Diagnostics.Debug.WriteLine("[EQ-SVC] Equalizer created, firing AvailabilityChanged");
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EQ-SVC] Equalizer creation failed: {ex.Message}");
            _isAvailable = false;
            _equalizer = null;
        }
    }

    private void DisposeEqualizer()
    {
        System.Diagnostics.Debug.WriteLine("[EQ-SVC] DisposeEqualizer called");
        _equalizer?.SetEnabled(false);
        _equalizer?.Release();
        _equalizer?.Dispose();
        _equalizer = null;
        _isAvailable = false;
    }

    /// <inheritdoc />
    public void SetBandLevel(int bandIndex, short gainMb)
        => _equalizer?.SetBandLevel((short)bandIndex, gainMb);

    /// <inheritdoc />
    public short[] GetBandLevels()
    {
        if (_equalizer is null)
            return [];
        var levels = new short[_equalizer.NumberOfBands];
        for (short i = 0; i < levels.Length; i++)
            levels[i] = _equalizer.GetBandLevel((short)i);
        return levels;
    }

    /// <inheritdoc />
    public void SetAllBands(IDictionary<string, double> bands)
    {
        if (_equalizer is null)
            return;

        var deviceFreqs = new int[_equalizer.NumberOfBands];
        for (int i = 0; i < deviceFreqs.Length; i++)
            deviceFreqs[i] = _equalizer.GetCenterFreq((short)i);

        foreach (var (freqLabel, gainDb) in bands)
        {
            var targetHz = ParseFrequencyLabel(freqLabel);
            var bandIdx = FindClosestBand(deviceFreqs, targetHz);
            if (bandIdx < 0)
                continue;
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
        if (_equalizer is null)
            return;
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
