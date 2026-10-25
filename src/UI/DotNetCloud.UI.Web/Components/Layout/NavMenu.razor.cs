using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Layout;

/// <summary>Code-behind for the <see cref="NavMenu"/> component.</summary>
public partial class NavMenu : ComponentBase, IDisposable
{
    [Parameter]
    public bool Collapsed { get; set; }

    protected override void OnInitialized()
    {
        ModuleRegistry.OnChange += StateHasChanged;
    }

    public void Dispose()
    {
        ModuleRegistry.OnChange -= StateHasChanged;
    }
}
