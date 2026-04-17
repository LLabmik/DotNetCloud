using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the channel list sidebar component.
/// </summary>
public partial class ChannelList : ComponentBase
{
    [Inject] private IChannelService ChannelService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    private string _searchQuery = string.Empty;
    private bool _isShowCreateChannel;
    private string _newChannelName = string.Empty;
    private string _newChannelType = "Public";
    private List<Guid> _pinnedOrder = [];
    private Guid? _draggingPinnedChannelId;
    private Guid? _dragOverPinnedChannelId;
    private bool _isLoadingInternal;
    private string? _errorMessageInternal;

    protected override Task OnInitializedAsync()
    {
        // Channel loading is managed by the parent ChatPageLayout component.
        // Loading independently here causes concurrent DbContext access.
        return Task.CompletedTask;
    }

    /// <summary>Event callback when a channel is selected.</summary>
    [Parameter]
    public EventCallback<ChannelViewModel> OnChannelSelected { get; set; }

    /// <summary>The list of channels to display.</summary>
    [Parameter]
    public List<ChannelViewModel> Channels { get; set; } = [];

    /// <summary>Event callback when a new channel should be created.</summary>
    [Parameter]
    public EventCallback<(string Name, string Type)> OnCreateChannel { get; set; }

    /// <summary>Event callback when pinned channel order changes.</summary>
    [Parameter]
    public EventCallback<IReadOnlyList<Guid>> OnChannelReordered { get; set; }

    /// <summary>Event callback when the user wants to start a new direct message.</summary>
    [Parameter]
    public EventCallback OnNewDm { get; set; }

    /// <summary>Whether channels are currently loading.</summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>Error message to display when channel operations fail.</summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the search query filter.</summary>
    protected string SearchQuery
    {
        get => _searchQuery;
        set => _searchQuery = value;
    }

    /// <summary>Whether to show the create channel dialog.</summary>
    protected bool IsShowCreateChannel => _isShowCreateChannel;

    /// <summary>New channel name input.</summary>
    protected string NewChannelName
    {
        get => _newChannelName;
        set => _newChannelName = value;
    }

    /// <summary>New channel type selection.</summary>
    protected string NewChannelType
    {
        get => _newChannelType;
        set => _newChannelType = value;
    }

    /// <summary>Filtered channels based on search query.</summary>
    protected List<ChannelViewModel> FilteredChannels =>
        string.IsNullOrWhiteSpace(_searchQuery)
            ? Channels
            : Channels.Where(c => c.Name.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>Pinned channels in user-defined order.</summary>
    protected List<ChannelViewModel> PinnedChannels =>
        OrderPinnedChannels(FilteredChannels.Where(c => c.IsPinned).ToList());

    /// <summary>Effective loading state combining external and internal loading flags.</summary>
    protected bool EffectiveIsLoading => IsLoading || _isLoadingInternal;

    /// <summary>Effective error message, preferring explicitly passed parameter values.</summary>
    protected string? EffectiveErrorMessage => string.IsNullOrWhiteSpace(ErrorMessage)
        ? _errorMessageInternal
        : ErrorMessage;

    protected override void OnParametersSet()
    {
        SyncPinnedOrderWithChannels();
    }

    /// <summary>Selects a channel and raises the callback.</summary>
    protected async Task SelectChannel(ChannelViewModel channel)
    {
        foreach (var c in Channels)
        {
            c.IsActive = false;
        }
        channel.IsActive = true;
        await OnChannelSelected.InvokeAsync(channel);
    }

    /// <summary>Shows the create channel dialog.</summary>
    protected void ShowCreateChannel()
    {
        _isShowCreateChannel = true;
        _newChannelName = string.Empty;
        _newChannelType = "Public";
    }

    /// <summary>Hides the create channel dialog.</summary>
    protected void HideCreateChannel()
    {
        _isShowCreateChannel = false;
    }

    /// <summary>Handles the "+" button click in the Direct Messages section to open the DM user picker.</summary>
    protected async Task HandleNewDmClick()
    {
        await OnNewDm.InvokeAsync();
    }

    /// <summary>Creates a new channel via callback.</summary>
    protected async Task CreateChannel()
    {
        if (string.IsNullOrWhiteSpace(_newChannelName))
        {
            return;
        }

        if (OnCreateChannel.HasDelegate)
        {
            await OnCreateChannel.InvokeAsync((_newChannelName, _newChannelType));
            _isShowCreateChannel = false;
            return;
        }

        try
        {
            _errorMessageInternal = null;
            var caller = await GetCallerContextAsync();
            var created = await ChannelService.CreateChannelAsync(new CreateChannelDto
            {
                Name = _newChannelName.Trim(),
                Type = _newChannelType
            }, caller);

            Channels.Add(ToViewModel(created));
            Channels = Channels
                .OrderByDescending(c => c.LastActivityAt ?? DateTime.MinValue)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            SyncPinnedOrderWithChannels();

            _isShowCreateChannel = false;
            _newChannelName = string.Empty;
        }
        catch (Exception ex)
        {
            _errorMessageInternal = ex.Message;
        }
    }

    private async Task LoadChannelsAsync()
    {
        try
        {
            _errorMessageInternal = null;
            _isLoadingInternal = true;

            var caller = await GetCallerContextAsync();
            var channels = await ChannelService.ListChannelsAsync(caller);
            Channels = channels.Select(ToViewModel).ToList();
            SyncPinnedOrderWithChannels();
        }
        catch (Exception ex)
        {
            _errorMessageInternal = ex.Message;
        }
        finally
        {
            _isLoadingInternal = false;
        }
    }

    /// <summary>Supports keyboard selection for channel items.</summary>
    protected async Task HandleChannelKeyDown(KeyboardEventArgs e, ChannelViewModel channel)
    {
        if (e.Key is "Enter" or " ")
        {
            await SelectChannel(channel);
        }
    }

    /// <summary>Starts dragging a pinned channel.</summary>
    protected void HandlePinnedDragStart(Guid channelId)
    {
        if (!_pinnedOrder.Contains(channelId))
        {
            return;
        }

        _draggingPinnedChannelId = channelId;
        _dragOverPinnedChannelId = channelId;
    }

    /// <summary>Tracks the pinned channel currently being hovered during drag.</summary>
    protected void HandlePinnedDragOver(Guid channelId)
    {
        if (_draggingPinnedChannelId is null || _draggingPinnedChannelId == channelId)
        {
            return;
        }

        _dragOverPinnedChannelId = channelId;
    }

    /// <summary>Finalizes the drag operation and clears temporary state.</summary>
    protected void HandlePinnedDragEnd()
    {
        _draggingPinnedChannelId = null;
        _dragOverPinnedChannelId = null;
    }

    /// <summary>Reorders pinned channels when dropped and raises callback with new order.</summary>
    protected async Task HandlePinnedDropAsync(Guid targetChannelId)
    {
        if (_draggingPinnedChannelId is null)
        {
            return;
        }

        var draggedChannelId = _draggingPinnedChannelId.Value;

        if (draggedChannelId == targetChannelId)
        {
            HandlePinnedDragEnd();
            return;
        }

        SyncPinnedOrderWithChannels();

        var draggedIndex = _pinnedOrder.IndexOf(draggedChannelId);
        var targetIndex = _pinnedOrder.IndexOf(targetChannelId);

        if (draggedIndex < 0 || targetIndex < 0)
        {
            HandlePinnedDragEnd();
            return;
        }

        _pinnedOrder.RemoveAt(draggedIndex);
        _pinnedOrder.Insert(targetIndex, draggedChannelId);

        if (OnChannelReordered.HasDelegate)
        {
            await OnChannelReordered.InvokeAsync(_pinnedOrder.AsReadOnly());
        }

        HandlePinnedDragEnd();
    }

    /// <summary>Gets CSS classes used to style drag source and current drop target.</summary>
    protected string GetPinnedDragClass(Guid channelId)
    {
        if (_draggingPinnedChannelId == channelId)
        {
            return "is-dragging";
        }

        if (_dragOverPinnedChannelId == channelId)
        {
            return "is-drop-target";
        }

        return string.Empty;
    }

    private List<ChannelViewModel> OrderPinnedChannels(List<ChannelViewModel> pinnedChannels)
    {
        if (pinnedChannels.Count == 0)
        {
            return pinnedChannels;
        }

        SyncPinnedOrderWithChannels();

        var orderIndex = _pinnedOrder
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        return pinnedChannels
            .OrderBy(c => orderIndex.GetValueOrDefault(c.Id, int.MaxValue))
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SyncPinnedOrderWithChannels()
    {
        var currentPinnedIds = Channels
            .Where(c => c.IsPinned)
            .Select(c => c.Id)
            .ToList();

        _pinnedOrder = _pinnedOrder
            .Where(currentPinnedIds.Contains)
            .ToList();

        foreach (var channelId in currentPinnedIds)
        {
            if (!_pinnedOrder.Contains(channelId))
            {
                _pinnedOrder.Add(channelId);
            }
        }
    }

    private async Task<CallerContext> GetCallerContextAsync()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
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

    private static ChannelViewModel ToViewModel(ChannelDto dto)
    {
        return new ChannelViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            Type = dto.Type,
            Topic = dto.Topic,
            LastActivityAt = dto.LastActivityAt,
            MemberCount = dto.MemberCount,
            PresenceStatus = dto.Type is "DirectMessage" or "Group" ? "Offline" : string.Empty,
            UnreadCount = 0,
            MentionCount = 0,
            IsActive = false
        };
    }

    private static string GetPresenceClass(ChannelViewModel channel)
    {
        if (channel.Type is not ("DirectMessage" or "Group"))
        {
            return "presence-offline";
        }

        return channel.PresenceStatus.ToLowerInvariant() switch
        {
            "online" => "presence-online",
            "away" => "presence-away",
            _ => "presence-offline"
        };
    }
}
