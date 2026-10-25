using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Layout;

/// <summary>Code-behind for the <see cref="DncCard"/> component.</summary>
public partial class DncCard : ComponentBase
{
    /// <summary>Card title displayed in the header.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Custom header content (overrides Title).</summary>
    [Parameter]
    public RenderFragment? Header { get; set; }

    /// <summary>Card body content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Optional footer content.</summary>
    [Parameter]
    public RenderFragment? Footer { get; set; }

    /// <summary>Additional CSS class.</summary>
    [Parameter]
    public string? CssClass { get; set; }
}
