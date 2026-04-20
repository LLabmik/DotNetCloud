using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the call controls toolbar component.
/// Provides mute, camera, screen share, background effects, and hang up controls.
/// </summary>
public partial class CallControls : ComponentBase
{
    private bool _showEffectsPanel;

    /// <summary>Whether the microphone is muted.</summary>
    [Parameter]
    public bool IsMuted { get; set; }

    /// <summary>Whether the camera is turned off.</summary>
    [Parameter]
    public bool IsCameraOff { get; set; }

    /// <summary>Whether screen sharing is active.</summary>
    [Parameter]
    public bool IsScreenSharing { get; set; }

    /// <summary>Whether background blur is active on local video.</summary>
    [Parameter]
    public bool IsBackgroundBlurred { get; set; }

    /// <summary>Whether background blur is supported by the client browser.</summary>
    [Parameter]
    public bool IsBlurSupported { get; set; }

    /// <summary>Whether a virtual background is currently active.</summary>
    [Parameter]
    public bool HasVirtualBackground { get; set; }

    /// <summary>URL of the currently active virtual background image (null if none).</summary>
    [Parameter]
    public string? ActiveVirtualBackgroundUrl { get; set; }

    /// <summary>Current blur intensity in pixels (1-50).</summary>
    [Parameter]
    public int BlurIntensity { get; set; } = 10;

    /// <summary>Available virtual background options.</summary>
    [Parameter]
    public IReadOnlyList<VirtualBackgroundOption> VirtualBackgrounds { get; set; } = [];

    /// <summary>Number of participants in the call.</summary>
    [Parameter]
    public int ParticipantCount { get; set; }

    /// <summary>Call duration in seconds.</summary>
    [Parameter]
    public int DurationSeconds { get; set; }

    /// <summary>Connection quality indicator (Good, Fair, Poor).</summary>
    [Parameter]
    public string? ConnectionQuality { get; set; }

    /// <summary>Whether controls are disabled (e.g., during connection setup).</summary>
    [Parameter]
    public bool IsDisabled { get; set; }

    /// <summary>Whether the hang-up button specifically is disabled (independent of media controls).</summary>
    [Parameter]
    public bool IsHangUpDisabled { get; set; }

    /// <summary>Whether the current user can add participants to the active call (host-only).</summary>
    [Parameter]
    public bool CanAddPeople { get; set; }

    /// <summary>Callback when mute is toggled.</summary>
    [Parameter]
    public EventCallback<bool> OnToggleMute { get; set; }

    /// <summary>Callback when camera is toggled.</summary>
    [Parameter]
    public EventCallback<bool> OnToggleCamera { get; set; }

    /// <summary>Callback when screen share is toggled.</summary>
    [Parameter]
    public EventCallback<bool> OnToggleScreenShare { get; set; }

    /// <summary>Callback when background blur is toggled.</summary>
    [Parameter]
    public EventCallback<bool> OnToggleBackgroundBlur { get; set; }

    /// <summary>Callback when blur intensity changes.</summary>
    [Parameter]
    public EventCallback<int> OnBlurIntensityChanged { get; set; }

    /// <summary>Callback when a virtual background is selected.</summary>
    [Parameter]
    public EventCallback<string?> OnVirtualBackgroundSelected { get; set; }

    /// <summary>Callback when hang up is clicked.</summary>
    [Parameter]
    public EventCallback OnHangUp { get; set; }

    /// <summary>Callback when add people is clicked.</summary>
    [Parameter]
    public EventCallback OnAddPeople { get; set; }

    /// <summary>Formatted call duration string (MM:SS or H:MM:SS).</summary>
    protected string FormattedDuration => FormatDuration(DurationSeconds);

    /// <summary>Toggles microphone mute state.</summary>
    protected async Task HandleToggleMute()
    {
        await OnToggleMute.InvokeAsync(!IsMuted);
    }

    /// <summary>Toggles camera on/off state.</summary>
    protected async Task HandleToggleCamera()
    {
        await OnToggleCamera.InvokeAsync(!IsCameraOff);
    }

    /// <summary>Toggles screen sharing state.</summary>
    protected async Task HandleToggleScreenShare()
    {
        await OnToggleScreenShare.InvokeAsync(!IsScreenSharing);
    }

    /// <summary>Toggles background blur state.</summary>
    protected async Task HandleToggleBackgroundBlur()
    {
        await OnToggleBackgroundBlur.InvokeAsync(!IsBackgroundBlurred);
    }

    /// <summary>Handles blur intensity slider input.</summary>
    protected async Task HandleBlurIntensityInput(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out var intensity))
        {
            await OnBlurIntensityChanged.InvokeAsync(intensity);
        }
    }

    /// <summary>Handles selecting a virtual background image.</summary>
    protected async Task HandleSelectVirtualBackground(string url)
    {
        _showEffectsPanel = false;
        await OnVirtualBackgroundSelected.InvokeAsync(url);
    }

    /// <summary>Handles selecting blur-only mode (no virtual background).</summary>
    protected async Task HandleSelectBlurOnly()
    {
        _showEffectsPanel = false;
        await OnToggleBackgroundBlur.InvokeAsync(true);
    }

    /// <summary>Handles clearing virtual background and blur.</summary>
    protected async Task HandleClearVirtualBackground()
    {
        _showEffectsPanel = false;
        await OnVirtualBackgroundSelected.InvokeAsync(null);
    }

    /// <summary>Toggles the effects settings panel visibility.</summary>
    protected void ToggleEffectsPanel()
    {
        _showEffectsPanel = !_showEffectsPanel;
    }

    /// <summary>Gets the effects button title based on current state.</summary>
    protected string GetEffectsButtonTitle()
    {
        if (HasVirtualBackground) return "Virtual background active";
        if (IsBackgroundBlurred) return "Background blur active";
        return "Enable background effects";
    }

    /// <summary>Gets the effects button label based on current state.</summary>
    protected string GetEffectsButtonLabel()
    {
        if (HasVirtualBackground) return "Virtual BG";
        if (IsBackgroundBlurred) return "Blur On";
        return "Effects";
    }

    /// <summary>Handles hang up button click.</summary>
    protected async Task HandleHangUp()
    {
        await OnHangUp.InvokeAsync();
    }

    /// <summary>Handles add people button click.</summary>
    protected async Task HandleAddPeople()
    {
        await OnAddPeople.InvokeAsync();
    }

    /// <summary>Gets the connection quality indicator emoji.</summary>
    protected string GetQualityIndicator()
    {
        return ConnectionQuality?.ToLowerInvariant() switch
        {
            "good" => "🟢",
            "fair" => "🟡",
            "poor" => "🔴",
            _ => "⚪"
        };
    }

    /// <summary>Formats seconds into MM:SS or H:MM:SS display.</summary>
    internal static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        return hours > 0
            ? $"{hours}:{minutes:D2}:{seconds:D2}"
            : $"{minutes:D2}:{seconds:D2}";
    }
}
