using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Navigation;

/// <summary>Code-behind for the <see cref="DncTopBar"/> component.</summary>
public partial class DncTopBar : ComponentBase
{
    /// <summary>Page title displayed on the left.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Custom content for the left side (overrides Title).</summary>
    [Parameter]
    public RenderFragment? LeftContent { get; set; }

    /// <summary>Content for the right side (theme toggle, user menu, etc.).</summary>
    [Parameter]
    public RenderFragment? RightContent { get; set; }
}
