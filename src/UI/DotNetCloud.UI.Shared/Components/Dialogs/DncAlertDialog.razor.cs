using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Dialogs;

/// <summary>Code-behind for the <see cref="DncAlertDialog"/> component.</summary>
public partial class DncAlertDialog : ComponentBase
{
    private bool _visible;

    /// <summary>Dialog title.</summary>
    [Parameter]
    public string Title { get; set; } = "Alert";

    /// <summary>Alert message.</summary>
    [Parameter]
    public string Message { get; set; } = string.Empty;

    /// <summary>Alert level controlling styling.</summary>
    [Parameter]
    public AlertLevel Level { get; set; } = AlertLevel.Warning;

    /// <summary>Text for the dismiss button.</summary>
    [Parameter]
    public string DismissText { get; set; } = "OK";

    /// <summary>Raised when the alert is dismissed.</summary>
    [Parameter]
    public EventCallback OnDismissed { get; set; }

    /// <summary>Shows the alert dialog.</summary>
    public void Show(string? message = null, string? title = null, AlertLevel? level = null)
    {
        if (message is not null) Message = message;
        if (title is not null) Title = title;
        if (level is not null) Level = level.Value;
        _visible = true;
        StateHasChanged();
    }

    private async Task Dismiss()
    {
        _visible = false;
        await OnDismissed.InvokeAsync();
        StateHasChanged();
    }
}
