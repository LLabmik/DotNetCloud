using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>Code-behind for the <see cref="Contacts"/> page.</summary>
public partial class Contacts : ComponentBase
{
    [SupplyParameterFromQuery(Name = "contactId")]
    public string? ContactId { get; set; }

    private Dictionary<string, object>? _parameters;

    protected override void OnParametersSet()
    {
        _parameters = string.IsNullOrEmpty(ContactId)
            ? null
            : new Dictionary<string, object> { ["ContactId"] = ContactId };
    }
}
