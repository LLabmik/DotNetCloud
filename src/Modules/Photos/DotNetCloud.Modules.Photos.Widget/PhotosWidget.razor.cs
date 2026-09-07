using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Photos.Widget;

/// <summary>
/// Home-page widget for the Photos module: the five most recently added photos for
/// the signed-in user.
/// </summary>
public partial class PhotosWidget : ComponentBase
{
    [Inject] private IPhotoService PhotoService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private readonly List<PhotoDto> _items = [];
    private bool _loading = true;
    private string? _error;
    private string EmptyMessage => "No photos yet.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var caller = await BuildCallerAsync();
            var recent = await PhotoService.GetRecentPhotosAsync(caller, 5);
            _items.AddRange(recent);
        }
        catch (Exception)
        {
            _error = "Unable to load photo data.";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Builds a deep link that opens the Photos module at the given photo.
    /// </summary>
    private static string HrefFor(PhotoDto item)
        => $"/apps/photos?photoId={item.Id}";

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
