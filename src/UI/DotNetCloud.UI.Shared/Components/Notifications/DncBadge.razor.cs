using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Notifications;

/// <summary>Code-behind for the <see cref="DncBadge"/> component.</summary>
public partial class DncBadge : ComponentBase
{
    /// <summary>Badge color variant (success, warning, danger, muted).</summary>
    [Parameter]
    public string Variant { get; set; } = "muted";

    /// <summary>Additional CSS class.</summary>
    [Parameter]
    public string? CssClass { get; set; }

    /// <summary>Badge content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
