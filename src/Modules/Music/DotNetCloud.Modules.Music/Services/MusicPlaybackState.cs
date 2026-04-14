using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Services;

namespace DotNetCloud.Modules.Music.UI;

/// <summary>
/// Scoped service that holds music playback state for a single Blazor circuit.
/// Survives page navigations so the global playbar can keep playing music.
/// </summary>
public sealed class MusicPlaybackState
{
    private readonly IPlaybackService _playbackService;
    private readonly IEqPresetService _eqPresetService;

    /// <summary>Raised when any playback state changes. UI components should call StateHasChanged.</summary>
    public event Action? OnChange;

    /// <summary>Raised when the now-playing track changes and JS audio should (re)start.</summary>
    public event Action<TrackDto>? OnTrackChanged;

    // ── Playback state ──

    /// <summary>Currently playing track, or null if nothing is playing.</summary>
    public TrackDto? NowPlaying { get; private set; }

    /// <summary>Whether audio is currently playing (not paused).</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Current playback position within the track.</summary>
    public TimeSpan PlaybackPosition { get; private set; }

    // ── Queue ──

    /// <summary>The playback queue.</summary>
    public List<TrackDto> Queue { get; } = [];

    /// <summary>Current index within the queue.</summary>
    public int QueueIndex { get; private set; } = -1;

    /// <summary>Whether shuffle mode is enabled.</summary>
    public bool Shuffle { get; private set; }

    /// <summary>Current repeat mode.</summary>
    public RepeatMode Repeat { get; private set; } = RepeatMode.Off;

    // ── Volume / Mute ──

    /// <summary>Volume level (0–100).</summary>
    public int Volume { get; private set; } = 80;

    /// <summary>Whether audio is muted.</summary>
    public bool Muted { get; private set; }

    // ── Equalizer ──

    /// <summary>10-band EQ gain values in dB (-12 to +12).</summary>
    public double[] EqBands { get; } = new double[10];

    /// <summary>The currently applied EQ preset ID, if any.</summary>
    public Guid? ActivePresetId { get; private set; }

    /// <summary>Cached EQ presets list. Loaded lazily on first EQ panel open.</summary>
    public List<EqPresetDto> EqPresets { get; } = [];

    /// <summary>Whether EQ presets have been loaded.</summary>
    public bool EqPresetsLoaded { get; private set; }

    // ── Panel visibility ──

    /// <summary>Whether the queue slide-out panel is open.</summary>
    public bool ShowQueue { get; private set; }

    /// <summary>Whether the EQ slide-out panel is open.</summary>
    public bool ShowEqualizer { get; private set; }

    /// <summary>Whether the save-preset dialog is open.</summary>
    public bool ShowSavePresetDialog { get; private set; }

    /// <summary>Band labels for the 10-band equalizer.</summary>
    public static readonly string[] BandLabels = ["31", "63", "125", "250", "500", "1K", "2K", "4K", "8K", "16K"];

    /// <summary>
    /// Initializes a new instance of <see cref="MusicPlaybackState"/>.
    /// </summary>
    public MusicPlaybackState(IPlaybackService playbackService, IEqPresetService eqPresetService)
    {
        _playbackService = playbackService;
        _eqPresetService = eqPresetService;
    }

    // ────────────────────────────────────────────────────────
    //  Playback control
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the current track and marks playback as active.
    /// The caller is responsible for JS interop (play audio URL).
    /// </summary>
    public void PlayTrack(TrackDto track)
    {
        NowPlaying = track;
        IsPlaying = true;
        PlaybackPosition = TimeSpan.Zero;

        if (!Queue.Any(t => t.Id == track.Id))
        {
            Queue.Add(track);
            QueueIndex = Queue.Count - 1;
        }
        else
        {
            QueueIndex = Queue.FindIndex(t => t.Id == track.Id);
        }

        NotifyChanged();
        OnTrackChanged?.Invoke(track);
    }

    /// <summary>
    /// Replaces the entire queue and starts playing the first track.
    /// </summary>
    public void PlayQueue(List<TrackDto> tracks)
    {
        if (tracks.Count == 0) return;
        Queue.Clear();
        Queue.AddRange(tracks);
        QueueIndex = 0;
        NowPlaying = tracks[0];
        IsPlaying = true;
        PlaybackPosition = TimeSpan.Zero;
        NotifyChanged();
        OnTrackChanged?.Invoke(tracks[0]);
    }

    /// <summary>Toggles between playing and paused.</summary>
    public void TogglePlayPause()
    {
        IsPlaying = !IsPlaying;
        NotifyChanged();
    }

    /// <summary>Sets the playing state explicitly.</summary>
    public void SetPlaying(bool playing)
    {
        if (IsPlaying == playing) return;
        IsPlaying = playing;
        NotifyChanged();
    }

    /// <summary>
    /// Advances to the next track in the queue. Returns the next track or null if queue ended.
    /// </summary>
    public TrackDto? PlayNext()
    {
        if (Queue.Count == 0) return null;

        if (Repeat == RepeatMode.One && NowPlaying is not null)
        {
            PlaybackPosition = TimeSpan.Zero;
            NotifyChanged();
            OnTrackChanged?.Invoke(NowPlaying);
            return NowPlaying;
        }

        if (Shuffle)
        {
            QueueIndex = Random.Shared.Next(Queue.Count);
        }
        else
        {
            QueueIndex++;
            if (QueueIndex >= Queue.Count)
            {
                QueueIndex = Repeat == RepeatMode.All ? 0 : Queue.Count - 1;
                if (Repeat == RepeatMode.Off)
                {
                    IsPlaying = false;
                    NotifyChanged();
                    return null;
                }
            }
        }

        NowPlaying = Queue[QueueIndex];
        IsPlaying = true;
        PlaybackPosition = TimeSpan.Zero;
        NotifyChanged();
        OnTrackChanged?.Invoke(NowPlaying);
        return NowPlaying;
    }

    /// <summary>
    /// Goes to the previous track or restarts the current one if past 3 seconds.
    /// Returns the track to play.
    /// </summary>
    public TrackDto? PlayPrevious()
    {
        if (Queue.Count == 0) return null;

        if (PlaybackPosition.TotalSeconds > 3)
        {
            PlaybackPosition = TimeSpan.Zero;
            NotifyChanged();
            if (NowPlaying is not null)
                OnTrackChanged?.Invoke(NowPlaying);
            return NowPlaying;
        }

        QueueIndex = Math.Max(0, QueueIndex - 1);
        NowPlaying = Queue[QueueIndex];
        IsPlaying = true;
        PlaybackPosition = TimeSpan.Zero;
        NotifyChanged();
        OnTrackChanged?.Invoke(NowPlaying);
        return NowPlaying;
    }

    /// <summary>Stops playback and clears the now-playing track.</summary>
    public void Stop()
    {
        NowPlaying = null;
        IsPlaying = false;
        PlaybackPosition = TimeSpan.Zero;
        ShowQueue = false;
        ShowEqualizer = false;
        ShowSavePresetDialog = false;
        NotifyChanged();
    }

    // ────────────────────────────────────────────────────────
    //  Position / Seek
    // ────────────────────────────────────────────────────────

    /// <summary>Updates the current playback position (called from JS time update callback).</summary>
    public void UpdatePosition(TimeSpan position)
    {
        PlaybackPosition = position;
        // No NotifyChanged here — the caller (GlobalMusicPlaybar) will call StateHasChanged directly
        // to avoid excessive event handler invocations from the 500ms polling interval.
    }

    /// <summary>Sets playback position (for seek operations). Raises OnChange.</summary>
    public void Seek(TimeSpan position)
    {
        PlaybackPosition = position;
        NotifyChanged();
    }

    /// <summary>Updates metadata duration from JS if the track had no duration stored.</summary>
    public void UpdateDurationFromMetadata(double durationSeconds)
    {
        if (NowPlaying is not null && NowPlaying.Duration.TotalSeconds < 1 && durationSeconds > 0)
        {
            NowPlaying = NowPlaying with { Duration = TimeSpan.FromSeconds(durationSeconds) };
            NotifyChanged();
        }
    }

    /// <summary>
    /// Updates the NowPlaying track reference without restarting audio.
    /// Used when track metadata changes (e.g. star toggle) while playing.
    /// </summary>
    public void UpdateNowPlaying(TrackDto track)
    {
        if (NowPlaying is not null && NowPlaying.Id == track.Id)
        {
            NowPlaying = track;
            NotifyChanged();
        }
    }

    // ────────────────────────────────────────────────────────
    //  Volume / Mute
    // ────────────────────────────────────────────────────────

    /// <summary>Sets the volume level (0–100).</summary>
    public void SetVolume(int volume)
    {
        Volume = Math.Clamp(volume, 0, 100);
        NotifyChanged();
    }

    /// <summary>Toggles mute on/off.</summary>
    public void ToggleMute()
    {
        Muted = !Muted;
        NotifyChanged();
    }

    // ────────────────────────────────────────────────────────
    //  Shuffle / Repeat
    // ────────────────────────────────────────────────────────

    /// <summary>Toggles shuffle mode.</summary>
    public void ToggleShuffle()
    {
        Shuffle = !Shuffle;
        NotifyChanged();
    }

    /// <summary>Cycles through repeat modes: Off → All → One → Off.</summary>
    public void CycleRepeat()
    {
        Repeat = Repeat switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            RepeatMode.One => RepeatMode.Off,
            _ => RepeatMode.Off
        };
        NotifyChanged();
    }

    // ────────────────────────────────────────────────────────
    //  Queue management
    // ────────────────────────────────────────────────────────

    /// <summary>Adds a track to the end of the queue.</summary>
    public void AddToQueue(TrackDto track)
    {
        Queue.Add(track);
        NotifyChanged();
    }

    /// <summary>Removes a track from the queue by index.</summary>
    public void RemoveFromQueue(int index)
    {
        if (index >= 0 && index < Queue.Count)
        {
            Queue.RemoveAt(index);
            if (index < QueueIndex) QueueIndex--;
            NotifyChanged();
        }
    }

    /// <summary>Jumps to and plays a specific index in the queue.</summary>
    public void PlayQueueAt(int index)
    {
        if (index < 0 || index >= Queue.Count) return;
        QueueIndex = index;
        NowPlaying = Queue[index];
        IsPlaying = true;
        PlaybackPosition = TimeSpan.Zero;
        NotifyChanged();
        OnTrackChanged?.Invoke(NowPlaying);
    }

    // ────────────────────────────────────────────────────────
    //  Equalizer
    // ────────────────────────────────────────────────────────

    /// <summary>Sets a single EQ band gain value.</summary>
    public void SetEqBand(int bandIndex, double gainDb)
    {
        if (bandIndex >= 0 && bandIndex < EqBands.Length)
        {
            EqBands[bandIndex] = gainDb;
            NotifyChanged();
        }
    }

    /// <summary>Resets all EQ bands to flat (0 dB).</summary>
    public void ResetEqBands()
    {
        ActivePresetId = null;
        Array.Clear(EqBands);
        NotifyChanged();
    }

    /// <summary>Applies an EQ preset.</summary>
    public void ApplyPreset(EqPresetDto preset)
    {
        ActivePresetId = preset.Id;
        if (preset.Bands is not null && preset.Bands.Count >= 10)
        {
            var bandValues = preset.Bands.Values.Take(10).ToArray();
            for (int i = 0; i < bandValues.Length; i++)
                EqBands[i] = bandValues[i];
        }
        NotifyChanged();
    }

    /// <summary>Loads EQ presets from the database (cached after first call).</summary>
    public async Task LoadEqPresetsAsync(CallerContext caller)
    {
        if (EqPresetsLoaded) return;
        try
        {
            var presets = await _eqPresetService.ListPresetsAsync(caller);
            EqPresets.Clear();
            EqPresets.AddRange(presets);
            EqPresetsLoaded = true;
        }
        catch
        {
            // Swallow — EQ is a nice-to-have, not critical
        }
    }

    /// <summary>Saves the current EQ bands as a named preset.</summary>
    public async Task<EqPresetDto?> SavePresetAsync(string name, CallerContext caller)
    {
        var bands = new Dictionary<string, double>();
        for (int i = 0; i < EqBands.Length && i < BandLabels.Length; i++)
        {
            bands[BandLabels[i]] = EqBands[i];
        }

        var dto = new SaveEqPresetDto { Name = name.Trim(), Bands = bands };
        var created = await _eqPresetService.CreatePresetAsync(dto, caller);
        EqPresets.Add(created);
        ActivePresetId = created.Id;
        ShowSavePresetDialog = false;
        NotifyChanged();
        return created;
    }

    // ────────────────────────────────────────────────────────
    //  Panel toggles
    // ────────────────────────────────────────────────────────

    /// <summary>Toggles the queue panel visibility.</summary>
    public void ToggleQueue()
    {
        ShowQueue = !ShowQueue;
        NotifyChanged();
    }

    /// <summary>Toggles the equalizer panel visibility.</summary>
    public void ToggleEqualizer()
    {
        ShowEqualizer = !ShowEqualizer;
        NotifyChanged();
    }

    /// <summary>Toggles the save-preset dialog visibility.</summary>
    public void ToggleSavePresetDialog()
    {
        ShowSavePresetDialog = !ShowSavePresetDialog;
        NotifyChanged();
    }

    // ────────────────────────────────────────────────────────
    //  Starring (delegates to IPlaybackService)
    // ────────────────────────────────────────────────────────

    /// <summary>Toggles the starred state of the currently playing track.</summary>
    public async Task ToggleStarNowPlayingAsync(CallerContext caller)
    {
        if (NowPlaying is null) return;
        await _playbackService.ToggleStarAsync(NowPlaying.Id, StarredItemType.Track, caller);
        NowPlaying = NowPlaying with { IsStarred = !NowPlaying.IsStarred };
        NotifyChanged();
    }

    /// <summary>Records a play event for the current track.</summary>
    public async Task RecordPlayAsync(CallerContext caller)
    {
        if (NowPlaying is null) return;
        await _playbackService.RecordPlayAsync(NowPlaying.Id, (int)NowPlaying.Duration.TotalSeconds, caller);
    }

    // ────────────────────────────────────────────────────────
    //  Progress helpers
    // ────────────────────────────────────────────────────────

    /// <summary>Gets the current playback progress as a percentage (0–100).</summary>
    public double GetProgressPercent()
    {
        if (NowPlaying is null || NowPlaying.Duration.TotalSeconds < 1) return 0;
        return PlaybackPosition.TotalSeconds / NowPlaying.Duration.TotalSeconds * 100;
    }

    /// <summary>Gets the audio content URL for a track via the Files module endpoint.</summary>
    public static string GetAudioUrl(TrackDto track) => $"/api/v1/files/{track.FileNodeId}/content";

    /// <summary>Gets the album art URL for an album.</summary>
    public static string GetAlbumArtUrl(Guid albumId) => $"/api/v1/music/albums/{albumId}/cover";

    /// <summary>Formats a duration as mm:ss or h:mm:ss.</summary>
    public static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes}:{duration.Seconds:D2}";
    }

    // ────────────────────────────────────────────────────────
    //  Internal
    // ────────────────────────────────────────────────────────

    private void NotifyChanged() => OnChange?.Invoke();
}

/// <summary>
/// Repeat mode for the music player queue.
/// </summary>
public enum RepeatMode
{
    /// <summary>No repeat.</summary>
    Off,

    /// <summary>Repeat entire queue.</summary>
    All,

    /// <summary>Repeat current track.</summary>
    One
}
