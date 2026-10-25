using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>Code-behind for the <see cref="TracksCard"/> page.</summary>
public partial class TracksCard : ComponentBase
{
    [Parameter] public int CardNumber { get; set; }

    private Type? _componentType;
    private Dictionary<string, object> _parameters = new();

    protected override void OnParametersSet()
    {
        _componentType = ModuleRegistry.GetPage("tracks.card");
        _parameters = new Dictionary<string, object>
        {
            ["CardNumber"] = CardNumber
        };
    }
}
