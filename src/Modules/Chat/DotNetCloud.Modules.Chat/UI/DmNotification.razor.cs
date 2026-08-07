using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the DM notification component.
/// Displays incoming DM channel creation notification with accept/reply/ignore/DND actions.
/// </summary>
public partial class DmNotification : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool _soundActive;

    /// <summary>Whether the notification is visible.</summary>
    [Parameter]
    public bool IsVisible { get; set; }

    /// <summary>Name of the initiator who started the DM.</summary>
    [Parameter]
    public string InitiatorName { get; set; } = string.Empty;

    /// <summary>Avatar URL of the initiator, if available.</summary>
    [Parameter]
    public string? InitiatorAvatarUrl { get; set; }

    /// <summary>Remaining seconds before auto-dismiss.</summary>
    [Parameter]
    public int RemainingSeconds { get; set; }

    /// <summary>Callback when the user accepts (reply &amp; join) the DM.</summary>
    [Parameter]
    public EventCallback OnAccept { get; set; }

    /// <summary>Callback when the user replies without joining.</summary>
    [Parameter]
    public EventCallback OnReply { get; set; }

    /// <summary>Callback when the user ignores the DM.</summary>
    [Parameter]
    public EventCallback OnIgnore { get; set; }

    /// <summary>Callback when the user enables Do Not Disturb.</summary>
    [Parameter]
    public EventCallback OnEnableDnd { get; set; }

    /// <summary>Callback when the user dismisses the notification.</summary>
    [Parameter]
    public EventCallback OnDismiss { get; set; }

    /// <summary>Gets initials from the initiator name for the avatar.</summary>
    internal static string GetInitials(string? name)
    {
        return VideoCallDialog.GetInitials(name);
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        if (IsVisible && !_soundActive)
        {
            _soundActive = true;
            try
            {
                await JS.InvokeVoidAsync("dotnetcloudRingtone.play", "sounds/message-tone.mp3", 0.4);
            }
            catch (JSDisconnectedException) { /* circuit gone */ }
        }
        else if (!IsVisible && _soundActive)
        {
            _soundActive = false;
            await StopSoundAsync();
        }
    }

    /// <summary>Handles accept button click.</summary>
    protected async Task HandleAccept()
    {
        await StopSoundAsync();
        await OnAccept.InvokeAsync();
    }

    /// <summary>Handles reply button click.</summary>
    protected async Task HandleReply()
    {
        await StopSoundAsync();
        await OnReply.InvokeAsync();
    }

    /// <summary>Handles ignore button click.</summary>
    protected async Task HandleIgnore()
    {
        await StopSoundAsync();
        await OnIgnore.InvokeAsync();
    }

    /// <summary>Handles DND button click.</summary>
    protected async Task HandleEnableDnd()
    {
        await StopSoundAsync();
        await OnEnableDnd.InvokeAsync();
    }

    /// <summary>Handles dismiss (X) button click.</summary>
    protected async Task HandleDismiss()
    {
        await StopSoundAsync();
        await OnDismiss.InvokeAsync();
    }

    private async Task StopSoundAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("dotnetcloudRingtone.stop");
        }
        catch (JSDisconnectedException) { /* circuit gone */ }
    }
}
