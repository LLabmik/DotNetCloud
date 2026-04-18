using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the channel header component.
/// </summary>
public partial class ChannelHeader : ComponentBase
{
    /// <summary>The currently selected channel.</summary>
    [Parameter]
    public ChannelViewModel? Channel { get; set; }

    /// <summary>Whether the current user is an admin or owner of this channel.</summary>
    [Parameter]
    public bool IsAdminOrOwner { get; set; }

    /// <summary>Callback to toggle the member list panel.</summary>
    [Parameter]
    public EventCallback OnToggleMemberList { get; set; }

    /// <summary>Callback to open search.</summary>
    [Parameter]
    public EventCallback OnSearch { get; set; }

    /// <summary>Callback to open edit-channel flow.</summary>
    [Parameter]
    public EventCallback<ChannelViewModel> OnEditChannel { get; set; }

    /// <summary>Callback to archive the current channel.</summary>
    [Parameter]
    public EventCallback<ChannelViewModel> OnArchiveChannel { get; set; }

    /// <summary>Callback to leave the current channel.</summary>
    [Parameter]
    public EventCallback<ChannelViewModel> OnLeaveChannel { get; set; }

    /// <summary>Callback when channel pin state changes.</summary>
    [Parameter]
    public EventCallback<(Guid ChannelId, bool IsPinned)> OnPinChanged { get; set; }

    /// <summary>Callback when channel mute state changes.</summary>
    [Parameter]
    public EventCallback<(Guid ChannelId, bool IsMuted)> OnMuteChanged { get; set; }

    /// <summary>Callback to open the invite dialog.</summary>
    [Parameter]
    public EventCallback OnInvite { get; set; }

    /// <summary>Callback to add people to DM/group channels.</summary>
    [Parameter]
    public EventCallback OnAddPeople { get; set; }

    /// <summary>Callback to start an audio call.</summary>
    [Parameter]
    public EventCallback OnAudioCall { get; set; }

    /// <summary>Callback to start a video call.</summary>
    [Parameter]
    public EventCallback OnVideoCall { get; set; }

    /// <summary>Callback to join an active call.</summary>
    [Parameter]
    public EventCallback OnJoinCall { get; set; }

    /// <summary>Callback to open call history panel.</summary>
    [Parameter]
    public EventCallback OnCallHistory { get; set; }

    /// <summary>Whether there is an active call in the channel.</summary>
    [Parameter]
    public bool HasActiveCall { get; set; }

    /// <summary>Toggles the member list panel.</summary>
    protected async Task ToggleMemberList()
    {
        await OnToggleMemberList.InvokeAsync();
    }

    /// <summary>Handles search button click.</summary>
    protected async Task OnSearchClick()
    {
        await OnSearch.InvokeAsync();
    }

    /// <summary>Handles edit button click.</summary>
    protected async Task OnEditClick()
    {
        if (Channel is not null)
        {
            await OnEditChannel.InvokeAsync(Channel);
        }
    }

    /// <summary>Handles archive button click.</summary>
    protected async Task OnArchiveClick()
    {
        if (Channel is not null)
        {
            await OnArchiveChannel.InvokeAsync(Channel);
        }
    }

    /// <summary>Handles leave button click.</summary>
    protected async Task OnLeaveClick()
    {
        if (Channel is not null)
        {
            await OnLeaveChannel.InvokeAsync(Channel);
        }
    }

    /// <summary>Handles invite button click.</summary>
    protected async Task OnInviteClick()
    {
        await OnInvite.InvokeAsync();
    }

    /// <summary>Handles add people button click.</summary>
    protected async Task OnAddPeopleClick()
    {
        await OnAddPeople.InvokeAsync();
    }

    /// <summary>Handles audio call button click.</summary>
    protected async Task OnAudioCallClick()
    {
        await OnAudioCall.InvokeAsync();
    }

    /// <summary>Handles video call button click.</summary>
    protected async Task OnVideoCallClick()
    {
        await OnVideoCall.InvokeAsync();
    }

    /// <summary>Handles join call button click.</summary>
    protected async Task OnJoinCallClick()
    {
        await OnJoinCall.InvokeAsync();
    }

    /// <summary>Handles call history button click.</summary>
    protected async Task OnCallHistoryClick()
    {
        await OnCallHistory.InvokeAsync();
    }

    private bool _showHelp;

    /// <summary>Toggles the help popover.</summary>
    protected void ToggleHelp()
    {
        _showHelp = !_showHelp;
    }

    /// <summary>Toggles pin state and raises callback.</summary>
    protected async Task OnTogglePinClick()
    {
        if (Channel is null)
        {
            return;
        }

        Channel.IsPinned = !Channel.IsPinned;
        await OnPinChanged.InvokeAsync((Channel.Id, Channel.IsPinned));
    }

    /// <summary>Toggles mute state and raises callback.</summary>
    protected async Task OnToggleMuteClick()
    {
        if (Channel is null)
        {
            return;
        }

        Channel.IsMuted = !Channel.IsMuted;
        await OnMuteChanged.InvokeAsync((Channel.Id, Channel.IsMuted));
    }
}
