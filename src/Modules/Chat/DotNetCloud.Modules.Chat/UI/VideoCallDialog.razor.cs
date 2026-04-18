using DotNetCloud.Modules.Chat.DTOs;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the video call dialog component.
/// Manages video grid layout, participant display, and call controls delegation.
/// </summary>
public partial class VideoCallDialog : ComponentBase
{
    /// <summary>Whether the dialog is visible.</summary>
    [Parameter]
    public bool IsVisible { get; set; }

    /// <summary>Name of the channel the call is in.</summary>
    [Parameter]
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>Current call state (Ringing, Connecting, Active, etc.).</summary>
    [Parameter]
    public string CallState { get; set; } = string.Empty;

    /// <summary>Local user display name.</summary>
    [Parameter]
    public string LocalDisplayName { get; set; } = string.Empty;

    /// <summary>Local user avatar URL, if available.</summary>
    [Parameter]
    public string? LocalAvatarUrl { get; set; }

    /// <summary>Current user ID.</summary>
    [Parameter]
    public Guid CurrentUserId { get; set; }

    /// <summary>Current call host user ID.</summary>
    [Parameter]
    public Guid HostUserId { get; set; }

    /// <summary>Remote participants in the call.</summary>
    [Parameter]
    public IReadOnlyList<CallParticipantDto> RemoteParticipants { get; set; } = [];

    /// <summary>Whether local microphone is muted.</summary>
    [Parameter]
    public bool IsMuted { get; set; }

    /// <summary>Whether local camera is off.</summary>
    [Parameter]
    public bool IsCameraOff { get; set; }

    /// <summary>Whether local screen sharing is active.</summary>
    [Parameter]
    public bool IsScreenSharing { get; set; }

    /// <summary>Call duration in seconds.</summary>
    [Parameter]
    public int DurationSeconds { get; set; }

    /// <summary>Connection quality indicator.</summary>
    [Parameter]
    public string? ConnectionQuality { get; set; }

    /// <summary>Callback when mute is toggled.</summary>
    [Parameter]
    public EventCallback<bool> OnToggleMute { get; set; }

    /// <summary>Callback when camera is toggled.</summary>
    [Parameter]
    public EventCallback<bool> OnToggleCamera { get; set; }

    /// <summary>Callback when screen share is toggled.</summary>
    [Parameter]
    public EventCallback<bool> OnToggleScreenShare { get; set; }

    /// <summary>Callback when hang up is clicked.</summary>
    [Parameter]
    public EventCallback OnHangUp { get; set; }

    /// <summary>Callback when add people is clicked.</summary>
    [Parameter]
    public EventCallback OnAddPeople { get; set; }

    /// <summary>Whether the add-people picker is currently visible.</summary>
    [Parameter]
    public bool ShowAddPeoplePicker { get; set; }

    /// <summary>Current add-people search term.</summary>
    [Parameter]
    public string AddPeopleSearchTerm { get; set; } = string.Empty;

    /// <summary>Whether add-people user search is in progress.</summary>
    [Parameter]
    public bool IsAddPeopleSearching { get; set; }

    /// <summary>User search results for add-people picker.</summary>
    [Parameter]
    public IReadOnlyList<UserSearchResultViewModel> AddPeopleSearchResults { get; set; } = [];

    /// <summary>Callback when add-people picker is closed.</summary>
    [Parameter]
    public EventCallback OnCloseAddPeoplePicker { get; set; }

    /// <summary>Callback when add-people search term changes.</summary>
    [Parameter]
    public EventCallback<string> OnAddPeopleSearchChanged { get; set; }

    /// <summary>Callback when a user is selected for call invitation.</summary>
    [Parameter]
    public EventCallback<Guid> OnInviteToCall { get; set; }

    /// <summary>Callback when host transfer is requested.</summary>
    [Parameter]
    public EventCallback<Guid> OnTransferHost { get; set; }

    /// <summary>Callback when minimize is clicked.</summary>
    [Parameter]
    public EventCallback OnMinimize { get; set; }

    /// <summary>Callback when picture-in-picture local video is clicked.</summary>
    [Parameter]
    public EventCallback OnPipClick { get; set; }

    /// <summary>Total participant count including local user.</summary>
    protected int TotalParticipantCount => RemoteParticipants.Count + 1;

    /// <summary>Whether the current user is the call host.</summary>
    protected bool IsCurrentUserHost => CurrentUserId != Guid.Empty && HostUserId == CurrentUserId;

    /// <summary>Whether call controls should be disabled.</summary>
    protected bool AreControlsDisabled => CallState is "Ringing" or "Ended" or "Failed" or "Missed" or "Rejected";

    /// <summary>Whether the hang-up button should be disabled. Hang-up is allowed during Ringing to cancel the call.</summary>
    protected bool IsHangUpDisabled => CallState is "Ended" or "Failed" or "Missed" or "Rejected";

    /// <summary>CSS class for dialog layout based on participant count.</summary>
    protected string LayoutClass => RemoteParticipants.Count switch
    {
        0 => "layout-solo",
        1 => "layout-pair",
        2 => "layout-trio",
        _ => "layout-grid"
    };

    /// <summary>Grid layout name for CSS targeting.</summary>
    protected string GridLayoutName => RemoteParticipants.Count switch
    {
        0 => "1x1",
        1 => "2x1",
        2 => "2x1-pip",
        _ => "2x2"
    };

    /// <summary>Formats call state for display.</summary>
    internal static string FormatCallState(string state)
    {
        return state switch
        {
            "Ringing" => "Ringing...",
            "Connecting" => "Connecting...",
            "Active" => "In Call",
            "OnHold" => "On Hold",
            "Ended" => "Call Ended",
            "Missed" => "Missed",
            "Rejected" => "Rejected",
            "Failed" => "Failed",
            _ => state
        };
    }

    /// <summary>Gets initials from a display name for avatar placeholder.</summary>
    internal static string GetInitials(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "?";
        }

        var parts = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }

        return parts[0].Length >= 2
            ? parts[0][..2].ToUpperInvariant()
            : parts[0].ToUpperInvariant();
    }

    /// <summary>Gets the waiting message based on call state.</summary>
    protected string GetWaitingMessage()
    {
        return CallState switch
        {
            "Ringing" => "Waiting for others to join...",
            "Connecting" => "Establishing connection...",
            _ => string.Empty
        };
    }

    /// <summary>Handles mute toggle from CallControls.</summary>
    protected async Task HandleToggleMute(bool muted)
    {
        await OnToggleMute.InvokeAsync(muted);
    }

    /// <summary>Handles camera toggle from CallControls.</summary>
    protected async Task HandleToggleCamera(bool cameraOff)
    {
        await OnToggleCamera.InvokeAsync(cameraOff);
    }

    /// <summary>Handles screen share toggle from CallControls.</summary>
    protected async Task HandleToggleScreenShare(bool sharing)
    {
        await OnToggleScreenShare.InvokeAsync(sharing);
    }

    /// <summary>Handles hang up from CallControls.</summary>
    protected async Task HandleHangUp()
    {
        await OnHangUp.InvokeAsync();
    }

    /// <summary>Handles add people button click.</summary>
    protected async Task HandleAddPeople()
    {
        await OnAddPeople.InvokeAsync();
    }

    /// <summary>Handles closing the add-people picker overlay.</summary>
    protected async Task HandleCloseAddPeoplePicker()
    {
        await OnCloseAddPeoplePicker.InvokeAsync();
    }

    /// <summary>Handles add-people search input changes.</summary>
    protected async Task HandleAddPeopleSearchInput(ChangeEventArgs args)
    {
        var term = args.Value?.ToString() ?? string.Empty;
        await OnAddPeopleSearchChanged.InvokeAsync(term);
    }

    /// <summary>Handles selecting a user to invite to the active call.</summary>
    protected async Task HandleInviteToCall(Guid userId)
    {
        await OnInviteToCall.InvokeAsync(userId);
    }

    /// <summary>Handles transfer host action for a selected participant.</summary>
    protected async Task HandleTransferHost(Guid userId)
    {
        await OnTransferHost.InvokeAsync(userId);
    }

    /// <summary>Handles minimize button click.</summary>
    protected async Task HandleMinimize()
    {
        await OnMinimize.InvokeAsync();
    }

    /// <summary>Handles click on picture-in-picture local video.</summary>
    protected async Task HandlePipClick()
    {
        await OnPipClick.InvokeAsync();
    }
}
