using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Notifications;

/// <summary>Code-behind for the <see cref="DncErrorDisplay"/> component.</summary>
public partial class DncErrorDisplay : ComponentBase
{
    /// <summary>Error heading text.</summary>
    [Parameter]
    public string Title { get; set; } = "Something went wrong";

    /// <summary>Optional exception to display the message from.</summary>
    [Parameter]
    public Exception? Exception { get; set; }

    /// <summary>Optional message (used when Exception is null).</summary>
    [Parameter]
    public string? Message { get; set; }

    /// <summary>Text for the dismiss/retry button.</summary>
    [Parameter]
    public string DismissText { get; set; } = "Try Again";

    /// <summary>Raised when the dismiss button is clicked.</summary>
    [Parameter]
    public EventCallback OnDismiss { get; set; }
}
