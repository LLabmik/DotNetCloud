using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Navigation;

/// <summary>Code-behind for the <see cref="DncSidebar"/> component.</summary>
public partial class DncSidebar : ComponentBase
{
    /// <summary>Brand icon (emoji or text) displayed at the top.</summary>
    [Parameter]
    public string BrandIcon { get; set; } = "☁";

    /// <summary>Brand text displayed next to the icon.</summary>
    [Parameter]
    public string BrandText { get; set; } = "DotNetCloud";

    /// <summary>Whether the sidebar is collapsed.</summary>
    [Parameter]
    public bool Collapsed { get; set; }

    /// <summary>Two-way binding for collapsed state.</summary>
    [Parameter]
    public EventCallback<bool> CollapsedChanged { get; set; }

    /// <summary>The navigation items to display.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<NavItem> Items { get; set; } = [];

    /// <summary>Optional additional content rendered after the main nav items.</summary>
    [Parameter]
    public RenderFragment? ExtraContent { get; set; }

    private async Task ToggleCollapsed()
    {
        Collapsed = !Collapsed;
        await CollapsedChanged.InvokeAsync(Collapsed);
    }
}
