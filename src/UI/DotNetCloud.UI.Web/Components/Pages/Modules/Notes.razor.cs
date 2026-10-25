using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>Code-behind for the <see cref="Notes"/> page.</summary>
public partial class Notes : ComponentBase
{
    [SupplyParameterFromQuery(Name = "noteId")]
    public string? NoteId { get; set; }

    private Dictionary<string, object>? _parameters;

    protected override void OnParametersSet()
    {
        _parameters = string.IsNullOrEmpty(NoteId)
            ? null
            : new Dictionary<string, object> { ["NoteId"] = NoteId };
    }
}
