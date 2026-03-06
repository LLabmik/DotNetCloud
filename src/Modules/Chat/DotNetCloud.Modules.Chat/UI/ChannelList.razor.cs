using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Chat.DTOs;
using DotNetCloud.Modules.Chat.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

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

    protected override async Task OnInitializedAsync()
    {
        if (Channels.Count == 0)
        {
            await LoadChannelsAsync();
        }
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

        _isShowCreateChannel = false;
        _newChannelName = string.Empty;
    }

    private async Task LoadChannelsAsync()
    {
        var caller = await GetCallerContextAsync();
        var channels = await ChannelService.ListChannelsAsync(caller);
        Channels = channels.Select(ToViewModel).ToList();
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
            UnreadCount = 0,
            MentionCount = 0,
            IsActive = false
        };
    }
}
