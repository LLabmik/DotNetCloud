namespace DotNetCloud.Modules.Music.UI;

/// <summary>
/// Scoped service that tracks which playlist (if any) is currently being played.
/// Used by the global playbar to display the playlist name and allow navigation.
/// </summary>
public sealed class ActivePlaylistContext
{
    /// <summary>Raised when the active playlist changes (set, cleared, or navigate requested).</summary>
    public event Action? OnPlaylistChanged;

    /// <summary>The currently active playlist ID, or null if not playing from a playlist.</summary>
    public Guid? PlaylistId { get; private set; }

    /// <summary>The currently active playlist name, or null if not playing from a playlist.</summary>
    public string? PlaylistName { get; private set; }

    /// <summary>
    /// Sets the active playlist context — call when starting playback from a playlist.
    /// </summary>
    public void SetPlaylist(Guid playlistId, string playlistName)
    {
        PlaylistId = playlistId;
        PlaylistName = playlistName;
        NotifyChanged();
    }

    /// <summary>
    /// Clears the active playlist context — call when starting playback from a non-playlist source
    /// (e.g., album, single track, search results, favorites, recently played).
    /// </summary>
    public void ClearPlaylist()
    {
        if (PlaylistId is null && PlaylistName is null)
            return;

        PlaylistId = null;
        PlaylistName = null;
        NotifyChanged();
    }

    /// <summary>
    /// Requests navigation to the currently tracked playlist.
    /// Called when the user clicks the playlist name in the playbar.
    /// </summary>
    public void RequestNavigateToPlaylist()
    {
        // Re-fire the event so subscribers (MusicPage) can navigate to this playlist
        NotifyChanged();
    }

    private void NotifyChanged() => OnPlaylistChanged?.Invoke();
}
