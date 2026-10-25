using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Layout;

/// <summary>Code-behind for the <see cref="DncPanel"/> component.</summary>
public partial class DncPanel : ComponentBase
{
    /// <summary>Panel heading text.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Panel content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter]
    public string? CssClass { get; set; }
}
