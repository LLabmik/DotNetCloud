using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Notifications;

/// <summary>Code-behind for the <see cref="DncLoadingIndicator"/> component.</summary>
public partial class DncLoadingIndicator : ComponentBase
{
    /// <summary>Whether the loading indicator is visible.</summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>Optional message displayed below the spinner.</summary>
    [Parameter]
    public string? Message { get; set; }
}
