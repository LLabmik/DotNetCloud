using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>Code-behind for the <see cref="DncBreadcrumb"/> component.</summary>
public partial class DncBreadcrumb : ComponentBase
{
    /// <summary>The breadcrumb trail items in order.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<BreadcrumbItem> Items { get; set; } = [];
}
