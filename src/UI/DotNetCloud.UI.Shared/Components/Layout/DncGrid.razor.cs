using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Layout;

/// <summary>Code-behind for the <see cref="DncGrid"/> component.</summary>
public partial class DncGrid : ComponentBase
{
    /// <summary>Number of columns (1-4). Maps to CSS grid classes.</summary>
    [Parameter]
    public int Columns { get; set; } = 1;

    /// <summary>Grid content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter]
    public string? CssClass { get; set; }
}
