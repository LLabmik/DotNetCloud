using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>Code-behind for the <see cref="TracksItem"/> page.</summary>
public partial class TracksItem : ComponentBase
{
    [Parameter] public Guid ProductId { get; set; }
    [Parameter] public int ItemNumber { get; set; }

    private Type? _componentType;
    private Dictionary<string, object> _parameters = new();

    protected override void OnParametersSet()
    {
        _componentType = ModuleRegistry.GetPage("tracks.item");
        _parameters = new Dictionary<string, object>
        {
            ["ProductId"] = ProductId,
            ["ItemNumber"] = ItemNumber
        };
    }
}
