using DotNetCloud.Core.DTOs;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Music.UI;

/// <summary>
/// Code-behind for <see cref="TrackActionsMenu"/> — the per-track "add to playlist" / "add to queue" menu.
/// </summary>
public partial class TrackActionsMenu
{
    /// <summary>The track the menu acts on.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>The caller's own playlists, offered as add targets.</summary>
    [Parameter]
    public IReadOnlyList<PlaylistDto> Playlists { get; set; } = Array.Empty<PlaylistDto>();

    /// <summary>
    /// Whether the dropdown is open. The page owns this state so that only one track menu is open at a time.
    /// </summary>
    [Parameter]
    public bool IsOpen { get; set; }

    /// <summary>Raised when the toggle button is clicked.</summary>
    [Parameter]
    public EventCallback OnToggle { get; set; }

    /// <summary>Raised when one of the caller's playlists is chosen.</summary>
    [Parameter]
    public EventCallback<PlaylistDto> OnAddToPlaylist { get; set; }

    /// <summary>Raised when "New playlist…" is chosen.</summary>
    [Parameter]
    public EventCallback OnNewPlaylist { get; set; }

    /// <summary>Raised when "Add to queue" is chosen.</summary>
    [Parameter]
    public EventCallback OnAddToQueue { get; set; }
}
