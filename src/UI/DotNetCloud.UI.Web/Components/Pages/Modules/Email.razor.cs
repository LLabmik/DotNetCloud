using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>Code-behind for the <see cref="Email"/> page.</summary>
public partial class Email : ComponentBase
{
    [SupplyParameterFromQuery(Name = "threadId")]
    public string? ThreadId { get; set; }

    private Dictionary<string, object>? _parameters;

    protected override void OnParametersSet()
    {
        _parameters = string.IsNullOrEmpty(ThreadId)
            ? null
            : new Dictionary<string, object> { ["ThreadId"] = ThreadId };
    }
}
