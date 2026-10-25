using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>Code-behind for the <see cref="ErrorDisplay"/> component.</summary>
public partial class ErrorDisplay : ComponentBase
{
    [Parameter]
    public Exception? Exception { get; set; }

    [Parameter]
    public EventCallback OnDismiss { get; set; }

    /// <summary>
    /// Text shown on the dismiss/retry button. Defaults to "Try Again".
    /// </summary>
    [Parameter]
    public string DismissText { get; set; } = "Try Again";
}
