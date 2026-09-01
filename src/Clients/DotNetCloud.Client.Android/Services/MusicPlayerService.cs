using Android.Content;
using Android.Media;
using DotNetCloud.Client.Core;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// <see cref="IMusicPlayerService"/> implementation wrapping <c>Android.Media.MediaPlayer</c>.
/// Supports streaming audio playback with a queue, seek, volume control, and foreground service integration.
/// </summary>
internal sealed class MusicPlayerService : IMusicPlayerService, IDisposable
{
    private readonly ILogger<MusicPlayerService> _logger;
    private MediaPlayer? _mediaPlayer;
    private Timer? _positionTimer;
    private string? _serverBaseUrl;
    private string? _accessToken;

    private readonly List<TrackDto> _queue = [];
    private int _queueIndex;

    /// <summary>Serializes access to PrepareAndStartAsync to prevent concurrent MediaPlayer setup.</summary>
    private readonly SemaphoreSlim _prepareLock = new(1, 1);

    /// <summary>The album ID the current queue was loaded from, if any.</summary>
    private Guid? _playingAlbumId;

    /// <summary>The playlist ID the current queue was loaded from, if any.</summary>
    private Guid? _playingPlaylistId;

    /// <summary>Current repeat mode — controls end-of-queue / track-end behavior.</summary>
    private RepeatMode _repeatMode;

    /// <summary>Re-entrancy guard for <see cref="Stop"/> to break error->Stop->error loops.</summary>
    private bool _isStopping;

    /// <summary>Consecutive <c>MEDIA_ERROR_SERVER_DIED</c> retries for the current track.</summary>
    private int _serverDiedRetries;

    /// <summary>Maximum consecutive -38 retries for the same track before skipping it.</summary>
    private const int MaxServerDiedRetries = 2;

    /// <summary>Some Samsung builds report <c>MEDIA_ERROR_SERVER_DIED</c> as -38 rather than 100.</summary>
    private const int MediaErrorServerDiedSamsung = -38;

    public MusicPlayerService(ILogger<MusicPlayerService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public TrackDto? CurrentTrack { get; private set; }

    /// <inheritdoc />
    public TimeSpan CurrentPosition =>
        TimeSpan.FromMilliseconds(_mediaPlayer?.CurrentPosition ?? 0);

    /// <inheritdoc />
    public TimeSpan Duration =>
        TimeSpan.FromMilliseconds(_mediaPlayer?.Duration ?? 0);

    /// <inheritdoc />
    public bool IsPlaying { get; private set; }

    /// <inheritdoc />
    public int AudioSessionId => _mediaPlayer?.AudioSessionId ?? 0;

    /// <inheritdoc />
    public Guid? PlayingAlbumId => _playingAlbumId;

    /// <inheritdoc />
    public Guid? PlayingPlaylistId => _playingPlaylistId;

    /// <inheritdoc />
    public RepeatMode RepeatMode => _repeatMode;

    /// <inheritdoc />
    public event EventHandler? PlaybackStateChanged;

    /// <inheritdoc />
    public event EventHandler? TrackStarted;

    /// <inheritdoc />
    public event EventHandler? TrackEnded;

    /// <inheritdoc />
    public event EventHandler? RepeatModeChanged;

    /// <inheritdoc />
    public async Task PlayAsync(TrackDto track, string serverBaseUrl, string accessToken)
    {
        _serverBaseUrl = serverBaseUrl;
        _accessToken = accessToken;
        CurrentTrack = track;

        // If the track is already in the queue (pre-populated by ViewModel),
        // just reposition to it without clearing the queue. Match by Id so a
        // logically-identical track instance (e.g. re-fetched metadata) still
        // repositions instead of collapsing the queue to a single standalone
        // track (which would stop playback after one song).
        var idx = _queue.FindIndex(t => t.Id == track.Id);
        if (idx >= 0)
        {
            _queueIndex = idx;
            System.Diagnostics.Debug.WriteLine($"[Music] PlayAsync: track {track.Id} found in queue at index {idx} (queue={_queue.Count})");
        }
        else
        {
            // Standalone playback — add as the only item and clear context
            _queue.Clear();
            _queue.Add(track);
            _queueIndex = 0;
            _playingAlbumId = null;
            _playingPlaylistId = null;
            System.Diagnostics.Debug.WriteLine($"[Music] PlayAsync: standalone play — queue reset to 1 item (queue={_queue.Count})");
        }

        await PrepareAndStartAsync().ConfigureAwait(false);
    }

    private async Task PrepareAndStartAsync()
    {
        if (CurrentTrack is null || _serverBaseUrl is null || _accessToken is null)
        {
            _logger.LogWarning("PrepareAndStartAsync: no current track/server/token — skipping");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[Music] PrepareAndStartAsync: BEGIN track={CurrentTrack.Id} index={_queueIndex} queue={_queue.Count} thread={Environment.CurrentManagedThreadId}");

        // Serialize MediaPlayer setup to prevent MEDIA_ERROR_SERVER_DIED (-38)
        // from rapid create/release cycles on Samsung/Android mediaserver.
        await _prepareLock.WaitAsync().ConfigureAwait(false);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Start foreground service IMMEDIATELY, before any async preparation
            // work. Android requires startForeground() within ~5s of
            // startForegroundService() or it crashes the app with
            // ForegroundServiceDidNotStartInTimeException.
            StartForegroundService();

            var audioUrl = $"{_serverBaseUrl.TrimEnd('/')}/api/v1/files/{CurrentTrack.FileNodeId}/content";
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {_accessToken}"
            };

            // Always use a fresh MediaPlayer per track. Reusing the same player via
            // Reset() between tracks fires MEDIA_ERROR_SERVER_DIED (-38) on some
            // Samsung devices — the mediaserver loses the player during the
            // transition and the next track never prepares. Android's documented
            // remedy for -38 is to release the player and instantiate a new one.
            _mediaPlayer?.Release();
            _mediaPlayer = null;
            var player = new MediaPlayer();
            player.Completion += OnTrackCompleted;
            player.Prepared += OnTrackPrepared;
            player.Error += OnPlayerError;
            _mediaPlayer = player;
            System.Diagnostics.Debug.WriteLine($"[Music] PrepareAndStartAsync: created fresh MediaPlayer t={sw.ElapsedMilliseconds}ms");

            var uri = global::Android.Net.Uri.Parse(audioUrl);
            if (uri is null)
            {
                _logger.LogError("Failed to parse audio URL for track {TrackId}", CurrentTrack.Id);
                System.Diagnostics.Debug.WriteLine($"[Music] PrepareAndStartAsync: FAILED to parse URL t={sw.ElapsedMilliseconds}ms");
                return;
            }

            _mediaPlayer.SetDataSource(
                global::Android.App.Application.Context,
                uri,
                headers);
            System.Diagnostics.Debug.WriteLine($"[Music] PrepareAndStartAsync: SetDataSource done t={sw.ElapsedMilliseconds}ms");

            _mediaPlayer.PrepareAsync();
            System.Diagnostics.Debug.WriteLine($"[Music] PrepareAndStartAsync: PrepareAsync sent t={sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare audio for track {TrackId}", CurrentTrack.Id);
            System.Diagnostics.Debug.WriteLine($"[Music] PrepareAndStartAsync: EXCEPTION t={sw.ElapsedMilliseconds}ms {ex}");
        }
        finally
        {
            _prepareLock.Release();
        }
    }

    private void OnTrackPrepared(object? sender, EventArgs e)
    {
        // Ignore stale Prepared events from players that have been replaced
        if (sender is not MediaPlayer player || player != _mediaPlayer)
            return;

        // A successful prepare resets the server-died retry counter.
        _serverDiedRetries = 0;
        System.Diagnostics.Debug.WriteLine($"[Music] OnTrackPrepared: track {CurrentTrack?.Id} ready t={Environment.TickCount64} — starting playback");
        player.Start();
        IsPlaying = true;
        StartPositionTimer();
        StartForegroundService();
        TrackStarted?.Invoke(this, EventArgs.Empty);
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTrackCompleted(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[Music] OnTrackCompleted: track {CurrentTrack?.Id} finished t={Environment.TickCount64} (index={_queueIndex}, queue={_queue.Count}, repeat={_repeatMode})");
        IsPlaying = false;
        StopPositionTimer();
        TrackEnded?.Invoke(this, EventArgs.Empty);
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        // Auto-advance is handled by the ViewModel's TrackEnded handler,
        // which dispatches PlayNextCommand on the main thread.
        // Do NOT call PlayNextIfQueued here — it would race with the ViewModel.
    }

    private void OnPlayerError(object? sender, MediaPlayer.ErrorEventArgs e)
    {
        _logger.LogError("MediaPlayer error: {What} (extra: {Extra})", e.What, e.Extra);
        System.Diagnostics.Debug.WriteLine($"[Music] OnPlayerError: what={e.What} extra={e.Extra}");

        // Do NOT call Stop() here: on some devices (Samsung) calling Stop() on a
        // player in the error state re-triggers the Error callback, producing an
        // infinite error→Stop→error loop that freezes the UI thread.
        var isServerDied = (int)e.What == MediaErrorServerDiedSamsung || e.What == MediaError.ServerDied;
        var failedTrack = CurrentTrack;
        var failedIndex = _queueIndex;

        IsPlaying = false;
        StopPositionTimer();

        // MEDIA_ERROR_SERVER_DIED (-38) means the mediaserver lost the player;
        // Android requires releasing and instantiating a new one. Release and null
        // it so the next prepare creates a fresh player.
        try { _mediaPlayer?.Release(); } catch { /* ignore */ }
        _mediaPlayer = null;

        if (isServerDied && failedTrack is not null && _serverDiedRetries < MaxServerDiedRetries)
        {
            // The mediaserver needs a fresh player + a brief moment to recover after
            // a release. Retry the SAME track instead of skipping it — otherwise
            // playback always jumps past the very next song.
            _serverDiedRetries++;
            System.Diagnostics.Debug.WriteLine($"[Music] OnPlayerError: SERVER_DIED — retry {_serverDiedRetries}/{MaxServerDiedRetries} for same track");
            _ = RetryPrepareAsync();
            return;
        }

        _serverDiedRetries = 0;
        CurrentTrack = null;
        _playingAlbumId = null;
        _playingPlaylistId = null;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        UpdateNotification();

        if (isServerDied)
        {
            // Retries exhausted — skip past the broken track so the queue continues.
            System.Diagnostics.Debug.WriteLine("[Music] OnPlayerError: SERVER_DIED — retries exhausted, skipping track");
            PlayNext();
        }
    }

    /// <summary>
    /// Retries preparing the current track with a fresh MediaPlayer after a
    /// <c>MEDIA_ERROR_SERVER_DIED</c>. Gives the mediaserver a brief moment to
    /// settle before the retry.
    /// </summary>
    private async Task RetryPrepareAsync()
    {
        await Task.Delay(250).ConfigureAwait(false);
        if (CurrentTrack is not null && _mediaPlayer is null)
            await PrepareAndStartAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Pause()
    {
        _mediaPlayer?.Pause();
        IsPlaying = false;
        StopPositionTimer();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        UpdateNotification();
    }

    /// <inheritdoc />
    public void Resume()
    {
        _mediaPlayer?.Start();
        IsPlaying = true;
        StartPositionTimer();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        UpdateNotification();
    }

    /// <inheritdoc />
    public void Stop()
    {
        // Re-entrancy guard: on some devices (Samsung) calling Stop() on a player
        // in an error/completed state fires the Error callback synchronously, which
        // calls Stop() again → an infinite error→Stop→error loop that freezes the
        // UI thread. Ignore re-entrant calls to break the loop.
        if (_isStopping)
            return;
        _isStopping = true;
        try
        {
            System.Diagnostics.Debug.WriteLine($"[Music] Stop: called t={Environment.TickCount64} (queue={_queue.Count})");
            _mediaPlayer?.Stop();
            // Keep the player alive for reuse via Reset()
            // Keep the foreground service alive — don't stop/restart it between tracks,
            // as rapid stop/start cycles can trigger ForegroundServiceDidNotStartInTimeException.
            IsPlaying = false;
            CurrentTrack = null;
            _playingAlbumId = null;
            _playingPlaylistId = null;
            StopPositionTimer();
            PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _isStopping = false;
        }
    }

    /// <inheritdoc />
    public void Seek(TimeSpan position)
    {
        _mediaPlayer?.SeekTo((int)position.TotalMilliseconds);
    }

    /// <inheritdoc />
    public void SetVolume(float volume)
    {
        _mediaPlayer?.SetVolume(volume, volume);
    }

    /// <inheritdoc />
    public void PlayNext()
    {
        System.Diagnostics.Debug.WriteLine($"[Music] PlayNext: called t={Environment.TickCount64} (index={_queueIndex}, queue={_queue.Count}, repeat={_repeatMode})");
        if (_queue.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("[Music] PlayNext: queue is EMPTY — nothing to advance to");
            return;
        }

        // Repeat One: replay the current track without advancing the queue
        if (_repeatMode == RepeatMode.One && CurrentTrack is not null)
        {
            System.Diagnostics.Debug.WriteLine("[Music] PlayNext: Repeat.One — replaying current track");
            _ = PrepareAndStartAsync();
            return;
        }

        // Normal advance
        _queueIndex++;
        if (_queueIndex >= _queue.Count)
        {
            if (_repeatMode == RepeatMode.All)
            {
                _queueIndex = 0;
                System.Diagnostics.Debug.WriteLine("[Music] PlayNext: Repeat.All — wrapped to index 0");
            }
            else
            {
                // RepeatMode.Off — stop at end of queue
                _queueIndex = _queue.Count - 1;
                System.Diagnostics.Debug.WriteLine("[Music] PlayNext: Repeat.Off at end of queue — STOPPING");
                Stop();
                return;
            }
        }

        CurrentTrack = _queue[_queueIndex];
        System.Diagnostics.Debug.WriteLine($"[Music] PlayNext: advancing to index={_queueIndex} track={CurrentTrack.Id}");
        _ = PrepareAndStartAsync();
    }

    /// <inheritdoc />
    public void PlayPrevious()
    {
        if (_queue.Count == 0)
            return;
        _queueIndex = (_queueIndex - 1 + _queue.Count) % _queue.Count;
        CurrentTrack = _queue[_queueIndex];
        _ = PrepareAndStartAsync();
    }

    /// <summary>
    /// Cycles through repeat modes: Off → One → All → Off.
    /// Fires <see cref="RepeatModeChanged"/> after the mode changes.
    /// </summary>
    public void CycleRepeat()
    {
        _repeatMode = _repeatMode switch
        {
            RepeatMode.Off => RepeatMode.One,
            RepeatMode.One => RepeatMode.All,
            RepeatMode.All => RepeatMode.Off,
            _ => RepeatMode.Off,
        };
        RepeatModeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Enqueue(IEnumerable<TrackDto> tracks)
    {
        _queue.AddRange(tracks);
    }

    /// <inheritdoc />
    public void ReplaceQueue(IEnumerable<TrackDto> tracks)
    {
        ReplaceQueue(tracks, null, null);
    }

    /// <inheritdoc />
    public void ReplaceQueue(IEnumerable<TrackDto> tracks, Guid? albumId, Guid? playlistId)
    {
        _queue.Clear();
        _queue.AddRange(tracks);
        _playingAlbumId = albumId;
        _playingPlaylistId = playlistId;
    }

    // ── Position timer ────────────────────────────────────────────────

    private void StartPositionTimer()
    {
        StopPositionTimer();
        _positionTimer = new Timer(
            _ => PlaybackStateChanged?.Invoke(this, EventArgs.Empty),
            null,
            TimeSpan.FromSeconds(0),
            TimeSpan.FromSeconds(1));
    }

    private void StopPositionTimer()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
    }

    // ── Foreground service helpers ─────────────────────────────────────

    private void StartForegroundService()
    {
        var intent = new Intent(global::Android.App.Application.Context, typeof(MusicPlaybackService));
        intent.SetAction(MusicPlaybackService.ActionStart);
        global::Android.App.Application.Context.StartForegroundService(intent);
    }

    private void StopForegroundService()
    {
        var intent = new Intent(global::Android.App.Application.Context, typeof(MusicPlaybackService));
        intent.SetAction(MusicPlaybackService.ActionStop);
        global::Android.App.Application.Context.StopService(intent);
    }

    private void UpdateNotification()
    {
        var intent = new Intent(global::Android.App.Application.Context, typeof(MusicPlaybackService));
        intent.SetAction(MusicPlaybackService.ActionUpdateNotification);
        global::Android.App.Application.Context.StartForegroundService(intent);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
        StopForegroundService();
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
        _prepareLock.Dispose();
    }
}
