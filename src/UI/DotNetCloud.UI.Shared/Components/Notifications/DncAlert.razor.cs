using DotNetCloud.UI.Shared.Components.Dialogs;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Notifications;

/// <summary>Code-behind for the <see cref="DncAlert"/> component.</summary>
public partial class DncAlert : ComponentBase
{
    /// <summary>Alert content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Alert severity level.</summary>
    [Parameter]
    public AlertLevel Level { get; set; } = AlertLevel.Warning;

    /// <summary>Whether the alert is visible.</summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>Two-way binding for visibility.</summary>
    [Parameter]
    public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>Whether the alert can be dismissed.</summary>
    [Parameter]
    public bool Dismissible { get; set; }

    private async Task DismissAlert()
    {
        Visible = false;
        await VisibleChanged.InvokeAsync(false);
    }
}
