using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Video.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Video.Widget;

/// <summary>
/// Home-page widget for the Video module: the five most recently added videos.
/// </summary>
public partial class VideoWidget : ComponentBase
{
    [Inject] private IVideoService VideoService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private readonly List<VideoDto> _items = [];
    private bool _loading = true;
    private string? _error;
    private string EmptyMessage => "No videos yet.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var caller = await BuildCallerAsync();
            var videos = await VideoService.GetRecentVideosAsync(caller, 0, 5);
            _items.AddRange(videos);
        }
        catch (Exception)
        {
            _error = "Unable to load video data.";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Builds a deep link that opens the Video module at the given video.
    /// </summary>
    private static string HrefFor(VideoDto item)
        => $"/apps/video?videoId={item.Id}";

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
