using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>Code-behind for the <see cref="Tracks"/> page.</summary>
public partial class Tracks : ComponentBase
{
    [SupplyParameterFromQuery(Name = "kanban")]
    public string? KanbanId { get; set; }

    private Dictionary<string, object>? _parameters;

    protected override void OnParametersSet()
    {
        _parameters = string.IsNullOrWhiteSpace(KanbanId)
            ? null
            : new Dictionary<string, object> { ["KanbanId"] = KanbanId };
    }
}
