using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;
using DotNetCloud.Modules.Chat.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DotNetCloud.Modules.Chat.Widget;

/// <summary>
/// Home-page widget for the Chat module: the caller's five most recently active channels,
/// with a live 4-state presence dot on each Direct Message row.
/// </summary>
public partial class ChatWidget : ComponentBase, IDisposable
{
    [Inject] private IChannelService ChannelService { get; set; } = default!;
    [Inject] private IPresenceTracker PresenceTracker { get; set; } = default!;
    [Inject] private IChatMessageNotifier ChatMessageNotifier { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private readonly List<ChannelDto> _items = [];
    private readonly Dictionary<Guid, string> _presenceByChannel = [];
    private Dictionary<Guid, Guid> _dmPeerMap = new();
    private bool _loading = true;
    private bool _isDisposed;
    private string? _error;
    private string EmptyMessage => "No active channels.";

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        ChatMessageNotifier.UserPresenceChanged += OnUserPresenceChanged;

        try
        {
            var caller = await BuildCallerAsync();
            var recent = await ChannelService.GetRecentChannelsAsync(caller, 5);
            _items.AddRange(recent);

            // Presence dots appear on Direct Message rows only — a Group row is not a single user.
            _dmPeerMap = ChannelPresenceMapping.BuildDmPeerMap(_items);
            if (_dmPeerMap.Count > 0)
            {
                var presenceStates = await PresenceTracker.GetOnlineStatusAsync(_dmPeerMap.Values.Distinct());
                foreach (var (channelId, status) in ChannelPresenceMapping.BuildPresenceMap(_dmPeerMap, presenceStates))
                {
                    _presenceByChannel[channelId] = status;
                }
            }
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

    private string GetPresenceClass(Guid channelId)
        => PresenceStatusHelpers.GetCssClass(_presenceByChannel.GetValueOrDefault(channelId));

    private string GetPresenceLabel(Guid channelId)
        => PresenceStatusHelpers.GetLabel(_presenceByChannel.GetValueOrDefault(channelId));

    private void OnUserPresenceChanged(UserPresenceChangedNotification notification)
    {
        if (_isDisposed)
        {
            return;
        }

        InvokeAsync(() =>
        {
            if (_isDisposed)
            {
                return;
            }

            var status = PresenceStatusHelpers.ToStatusString(notification.Status);
            var changed = false;
            foreach (var (channelId, peerId) in _dmPeerMap)
            {
                if (peerId == notification.UserId)
                {
                    _presenceByChannel[channelId] = status;
                    changed = true;
                }
            }

            if (changed)
            {
                StateHasChanged();
            }
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _isDisposed = true;
        ChatMessageNotifier.UserPresenceChanged -= OnUserPresenceChanged;
    }

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
