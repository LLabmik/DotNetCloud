using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Music.Widget;

/// <summary>
/// Home-page widget for the Music module: the five most recently added albums.
/// </summary>
public partial class MusicWidget : ComponentBase
{
    [Inject] private IMusicAlbumService AlbumService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private readonly List<MusicAlbumDto> _items = [];
    private bool _loading = true;
    private string? _error;
    private string EmptyMessage => "No albums yet.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var caller = await BuildCallerAsync();
            var albums = await AlbumService.GetRecentAlbumsAsync(caller, 5);
            _items.AddRange(albums);
        }
        catch (Exception)
        {
            _error = "Unable to load album data.";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Builds a deep link that opens the Music module at the given album.
    /// </summary>
    private static string HrefFor(MusicAlbumDto item)
        => $"/apps/music?albumId={item.Id}";

    private async Task<CallerContext> BuildCallerAsync()
    {
        var state = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = state.User;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing or invalid.");
        }

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(userId, roles, CallerType.User);
    }
}
