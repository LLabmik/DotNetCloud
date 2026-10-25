using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Navigation;

/// <summary>Code-behind for the <see cref="DncMenu"/> component.</summary>
public partial class DncMenu : ComponentBase
{
    /// <summary>The menu items.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<NavItem> Items { get; set; } = [];

    /// <summary>Additional CSS class.</summary>
    [Parameter]
    public string? CssClass { get; set; }
}
