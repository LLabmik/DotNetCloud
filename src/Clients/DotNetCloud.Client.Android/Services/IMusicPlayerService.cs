using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Client.Android.Services;

/// <summary>
/// Manages audio playback via <c>Android.Media.MediaPlayer</c>.
/// Supports play, pause, resume, seek, volume, and a playback queue.
/// </summary>
public interface IMusicPlayerService
{
    /// <summary>Plays the specified track, streaming from the server.</summary>
    Task PlayAsync(TrackDto track, string serverBaseUrl, string accessToken);

    /// <summary>Pauses playback.</summary>
    void Pause();

    /// <summary>Resumes paused playback.</summary>
    void Resume();

    /// <summary>Stops playback and releases the player.</summary>
    void Stop();

    /// <summary>Seeks to the specified position.</summary>
    void Seek(TimeSpan position);

    /// <summary>Sets the volume (0.0 = silent, 1.0 = full).</summary>
    void SetVolume(float volume);

    /// <summary>Advances to the next track in the queue.</summary>
    void PlayNext();

    /// <summary>Returns to the previous track in the queue.</summary>
    void PlayPrevious();

    /// <summary>Appends tracks to the end of the playback queue.</summary>
    void Enqueue(IEnumerable<TrackDto> tracks);

    /// <summary>The currently playing track, or null if stopped.</summary>
    TrackDto? CurrentTrack { get; }

    /// <summary>Current playback position.</summary>
    TimeSpan CurrentPosition { get; }

    /// <summary>Duration of the current track.</summary>
    TimeSpan Duration { get; }

    /// <summary>Whether audio is currently playing.</summary>
    bool IsPlaying { get; }

    /// <summary>The Android audio session ID used by the MediaPlayer.</summary>
    int AudioSessionId { get; }

    /// <summary>Raised when playback state (playing/paused/stopped) changes.</summary>
    event EventHandler? PlaybackStateChanged;

    /// <summary>Raised when the current track finishes playing.</summary>
    event EventHandler? TrackEnded;
}
