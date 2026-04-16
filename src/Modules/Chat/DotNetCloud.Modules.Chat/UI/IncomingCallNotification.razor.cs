using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the incoming call notification component.
/// Displays caller information with accept/reject actions and ring timeout.
/// </summary>
public partial class IncomingCallNotification : ComponentBase
{
    /// <summary>Whether the notification is visible.</summary>
    [Parameter]
    public bool IsVisible { get; set; }

    /// <summary>Name of the caller.</summary>
    [Parameter]
    public string CallerName { get; set; } = string.Empty;

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

    /// <summary>Handles accept with video button click.</summary>
    protected async Task HandleAcceptVideo()
    {
        await OnAcceptVideo.InvokeAsync();
    }

    /// <summary>Handles accept with audio only button click.</summary>
    protected async Task HandleAcceptAudio()
    {
        await OnAcceptAudio.InvokeAsync();
    }

    /// <summary>Handles reject button click.</summary>
    protected async Task HandleReject()
    {
        await OnReject.InvokeAsync();
    }
}
