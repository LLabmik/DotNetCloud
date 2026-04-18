using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the incoming call notification component.
/// Displays caller information with accept/reject actions and ring timeout.
/// </summary>
public partial class IncomingCallNotification : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _ringtoneActive;

    /// <summary>Whether the notification is visible.</summary>
    [Parameter]
    public bool IsVisible { get; set; }

    /// <summary>Name of the caller.</summary>
    [Parameter]
    public string CallerName { get; set; } = string.Empty;

    /// <summary>Avatar URL of the caller, if available.</summary>
    [Parameter]
    public string? CallerAvatarUrl { get; set; }

    /// <summary>Name of the channel the call is from.</summary>
    [Parameter]
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>Media type of the incoming call (Audio or Video).</summary>
    [Parameter]
    public string MediaType { get; set; } = "Audio";

    /// <summary>Remaining seconds before ring timeout auto-dismiss (0 = no timer shown).</summary>
    [Parameter]
    public int RemainingSeconds { get; set; }

    /// <summary>Whether the ringing animation should play.</summary>
    [Parameter]
    public bool IsRinging { get; set; } = true;

    /// <summary>Whether this is a mid-call invite (joining an ongoing call) vs a fresh call.</summary>
    [Parameter]
    public bool IsMidCallInvite { get; set; }

    /// <summary>Number of participants currently in the call (for mid-call invites).</summary>
    [Parameter]
    public int ParticipantCount { get; set; }

    /// <summary>Callback when the call is accepted with video.</summary>
    [Parameter]
    public EventCallback OnAcceptVideo { get; set; }

    /// <summary>Callback when the call is accepted with audio only.</summary>
    [Parameter]
    public EventCallback OnAcceptAudio { get; set; }

    /// <summary>Callback when the call is rejected.</summary>
    [Parameter]
    public EventCallback OnReject { get; set; }

    /// <summary>Gets initials from the caller name for the avatar.</summary>
    internal static string GetInitials(string? name)
    {
        return VideoCallDialog.GetInitials(name);
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (IsVisible && !_ringtoneActive)
        {
            _ringtoneActive = true;
            try
            {
                await JS.InvokeVoidAsync("dotnetcloudRingtone.play", "sounds/computer-ambience.mp3", 0.6);
            }
            catch (JSDisconnectedException) { /* circuit gone */ }
        }
        else if (!IsVisible && _ringtoneActive)
        {
            _ringtoneActive = false;
            await StopRingtoneAsync();
        }
    }

    /// <summary>Handles accept with video button click.</summary>
    protected async Task HandleAcceptVideo()
    {
        await StopRingtoneAsync();
        await OnAcceptVideo.InvokeAsync();
    }

    /// <summary>Handles accept with audio only button click.</summary>
    protected async Task HandleAcceptAudio()
    {
        await StopRingtoneAsync();
        await OnAcceptAudio.InvokeAsync();
    }

    /// <summary>Handles reject button click.</summary>
    protected async Task HandleReject()
    {
        await StopRingtoneAsync();
        await OnReject.InvokeAsync();
    }

    private async Task StopRingtoneAsync()
    {
        _ringtoneActive = false;
        try
        {
            await JS.InvokeVoidAsync("dotnetcloudRingtone.stop");
        }
        catch (JSDisconnectedException) { /* circuit gone */ }
    }
}
