using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Data.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Tracks.Widget;

/// <summary>
/// Home-page widget for the Tracks module: the caller's upcoming work items due within
/// the next fourteen days that are assigned to or watched by them.
/// </summary>
public partial class TracksWidget : ComponentBase
{
    [Inject] private WorkItemService WorkItemService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private readonly List<WorkItemDto> _items = [];
    private bool _loading = true;
    private string? _error;
    private string EmptyMessage => "No upcoming work items.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var caller = await BuildCallerAsync();
            var upcoming = await WorkItemService.GetMyUpcomingDueItemsAsync(caller.UserId, 5);
            _items.AddRange(upcoming);
        }
        catch (Exception)
        {
            _error = "Unable to load tracks data.";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Builds a deep link that opens the Tracks module at the given work item.
    /// </summary>
    private static string HrefFor(WorkItemDto item)
        => $"/apps/tracks/item/{item.ProductId}/{item.ItemNumber}";

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
