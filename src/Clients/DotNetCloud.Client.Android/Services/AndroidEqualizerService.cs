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

    /// <summary>Server preset target frequencies in Hz (10-band standard).</summary>
    internal static readonly int[] ServerFrequencies = [31, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    /// <summary>
    /// Cached mapping from virtual band index (0–9) to physical band index.
    /// Populated when the equalizer is created. -1 means unmapped.
    /// </summary>
    private int[] _virtualToPhysicalMap = [];

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
    public int VirtualBandCount => ServerFrequencies.Length;

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

    /// <inheritdoc />
    public int[] GetVirtualBandFrequenciesHz() => (int[])ServerFrequencies.Clone();

    /// <inheritdoc />
    public void SetVirtualBandGain(int virtualBandIndex, float gainDb)
    {
        if (_equalizer is null || virtualBandIndex < 0 || virtualBandIndex >= _virtualToPhysicalMap.Length)
            return;
        var physicalIdx = _virtualToPhysicalMap[virtualBandIndex];
        if (physicalIdx < 0)
            return;
        var gainMb = (short)Math.Clamp((int)(gainDb * 100), -1500, 1500);
        _equalizer.SetBandLevel((short)physicalIdx, gainMb);
    }

    /// <inheritdoc />
    public float[] GetVirtualBandGainsDb()
    {
        var result = new float[ServerFrequencies.Length];
        if (_equalizer is null)
            return result;
        for (int i = 0; i < ServerFrequencies.Length; i++)
        {
            var physicalIdx = i < _virtualToPhysicalMap.Length ? _virtualToPhysicalMap[i] : -1;
            if (physicalIdx >= 0)
                result[i] = _equalizer.GetBandLevel((short)physicalIdx) / 100f;
        }
        return result;
    }

    /// <inheritdoc />
    public void ApplyVirtualBandGains(float[] gainsDb)
    {
        if (_equalizer is null)
            return;
        var applied = new HashSet<short>();
        for (int i = 0; i < gainsDb.Length && i < _virtualToPhysicalMap.Length; i++)
        {
            var physicalIdx = (short)_virtualToPhysicalMap[i];
            if (physicalIdx < 0 || !applied.Add(physicalIdx))
                continue;
            var gainMb = (short)Math.Clamp((int)(gainsDb[i] * 100), -1500, 1500);
            _equalizer.SetBandLevel(physicalIdx, gainMb);
        }
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
        // AudioEffect must be created on the main looper thread on some Android versions.
        if (!MainThread.IsMainThread)
        {
            System.Diagnostics.Debug.WriteLine("[EQ-SVC] CreateEqualizer called from non-UI thread, dispatching");
            MainThread.BeginInvokeOnMainThread(CreateEqualizer);
            return;
        }

        try
        {
            DisposeEqualizer();
            _equalizer = new Equalizer(0, _player.AudioSessionId);
            _equalizer.SetEnabled(true);
            _isAvailable = true;

            // Build virtual→physical band mapping cache
            BuildVirtualBandMap();

            System.Diagnostics.Debug.WriteLine("[EQ-SVC] Equalizer created, firing AvailabilityChanged");
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EQ-SVC] Equalizer creation failed: {ex.Message}");
            _isAvailable = false;
            _equalizer = null;
            _virtualToPhysicalMap = [];
            // Notify ViewModel that EQ state changed (creation failed means unavailable)
            AvailabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DisposeEqualizer()
    {
        if (!_isAvailable && _equalizer is null)
            return; // already disposed, avoid redundant event firing

        System.Diagnostics.Debug.WriteLine("[EQ-SVC] DisposeEqualizer called");
        _equalizer?.SetEnabled(false);
        _equalizer?.Release();
        _equalizer?.Dispose();
        _equalizer = null;
        _isAvailable = false;
        // Notify ViewModel that EQ is gone so it hides sliders and shows the banner
        AvailabilityChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>
    /// Builds the cached mapping from each of the 10 server-standard frequencies
    /// to the closest physical device band. Called once when the equalizer is created.
    /// </summary>
    private void BuildVirtualBandMap()
    {
        if (_equalizer is null)
        {
            _virtualToPhysicalMap = [];
            return;
        }

        var physicalFreqsHz = new int[_equalizer.NumberOfBands];
        for (int i = 0; i < physicalFreqsHz.Length; i++)
            physicalFreqsHz[i] = _equalizer.GetCenterFreq((short)i) / 1000;

        _virtualToPhysicalMap = new int[ServerFrequencies.Length];
        for (int v = 0; v < ServerFrequencies.Length; v++)
        {
            var targetHz = ServerFrequencies[v];
            int bestIdx = -1, bestDiff = int.MaxValue;
            for (int p = 0; p < physicalFreqsHz.Length; p++)
            {
                var diff = Math.Abs(physicalFreqsHz[p] - targetHz);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestIdx = p;
                }
            }
            _virtualToPhysicalMap[v] = bestIdx;
            System.Diagnostics.Debug.WriteLine(
                $"[EQ-SVC] Virtual band {v} ({targetHz}Hz) → physical band {bestIdx} ({physicalFreqsHz[bestIdx]}Hz)");
        }
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
