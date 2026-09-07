using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Chat.Widget;

/// <summary>
/// Home-page widget for the Chat module: the caller's five most recently active channels.
/// </summary>
public partial class ChatWidget : ComponentBase
{
    [Inject] private IChannelService ChannelService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private readonly List<ChannelDto> _items = [];
    private bool _loading = true;
    private string? _error;
    private string EmptyMessage => "No active channels.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            var caller = await BuildCallerAsync();
            var recent = await ChannelService.GetRecentChannelsAsync(caller, 5);
            _items.AddRange(recent);
        }
        catch (Exception)
        {
            _error = "Unable to load chat data.";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// Builds a deep link that opens the Chat module at the given channel.
    /// </summary>
    private static string HrefFor(ChannelDto item)
        => $"/apps/chat?channelId={item.Id}";

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
