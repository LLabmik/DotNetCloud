using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Layout;

/// <summary>Code-behind for the <see cref="DncSection"/> component.</summary>
public partial class DncSection : ComponentBase
{
    /// <summary>Section heading text.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Action buttons rendered in the header area.</summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <summary>Section content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter]
    public string? CssClass { get; set; }
}
