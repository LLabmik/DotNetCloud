using Android.Content;
using Android.Media;
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
    public event EventHandler? PlaybackStateChanged;

    /// <inheritdoc />
    public event EventHandler? TrackEnded;

    /// <inheritdoc />
    public async Task PlayAsync(TrackDto track, string serverBaseUrl, string accessToken)
    {
        _serverBaseUrl = serverBaseUrl;
        _accessToken = accessToken;
        CurrentTrack = track;

        // If the track is already in the queue (pre-populated by ViewModel),
        // just reposition to it without clearing the queue.
        var idx = _queue.IndexOf(track);
        if (idx >= 0)
        {
            _queueIndex = idx;
        }
        else
        {
            // Standalone playback — add as the only item
            _queue.Clear();
            _queue.Add(track);
            _queueIndex = 0;
        }

        await PrepareAndStartAsync().ConfigureAwait(false);
    }

    private async Task PrepareAndStartAsync()
    {
        if (CurrentTrack is null || _serverBaseUrl is null || _accessToken is null)
            return;

        try
        {
            _mediaPlayer?.Release();
            _mediaPlayer = new MediaPlayer();

            var audioUrl = $"{_serverBaseUrl.TrimEnd('/')}/api/v1/files/{CurrentTrack.FileNodeId}/content";
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {_accessToken}"
            };

            _mediaPlayer.Completion += OnTrackCompleted;
            _mediaPlayer.Prepared += OnTrackPrepared;
            _mediaPlayer.Error += OnPlayerError;

            var uri = global::Android.Net.Uri.Parse(audioUrl);
            if (uri is null)
            {
                _logger.LogError("Failed to parse audio URL for track {TrackId}", CurrentTrack.Id);
                return;
            }
            _mediaPlayer.SetDataSource(
                global::Android.App.Application.Context,
                uri,
                headers);

            _mediaPlayer.PrepareAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prepare audio for track {TrackId}", CurrentTrack.Id);
        }
    }

    private void OnTrackPrepared(object? sender, EventArgs e)
    {
        _mediaPlayer?.Start();
        IsPlaying = true;
        StartPositionTimer();
        StartForegroundService();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTrackCompleted(object? sender, EventArgs e)
    {
        IsPlaying = false;
        StopPositionTimer();
        TrackEnded?.Invoke(this, EventArgs.Empty);
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PlayNextIfQueued();
    }

    private void OnPlayerError(object? sender, MediaPlayer.ErrorEventArgs e)
    {
        _logger.LogError("MediaPlayer error: {What} (extra: {Extra})", e.What, e.Extra);
        Stop();
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
        _mediaPlayer?.Stop();
        _mediaPlayer?.Release();
        _mediaPlayer = null;
        IsPlaying = false;
        CurrentTrack = null;
        StopPositionTimer();
        StopForegroundService();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
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
        if (_queue.Count == 0)
            return;
        _queueIndex = (_queueIndex + 1) % _queue.Count;
        CurrentTrack = _queue[_queueIndex];
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

    /// <inheritdoc />
    public void Enqueue(IEnumerable<TrackDto> tracks)
    {
        _queue.AddRange(tracks);
    }

    /// <inheritdoc />
    public void ReplaceQueue(IEnumerable<TrackDto> tracks)
    {
        _queue.Clear();
        _queue.AddRange(tracks);
    }

    private void PlayNextIfQueued()
    {
        if (_queue.Count > 1 && _queueIndex < _queue.Count - 1)
        {
            MainThread.BeginInvokeOnMainThread(() => PlayNext());
        }
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
        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
    }
}
