using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>Code-behind for the <see cref="Chat"/> page.</summary>
public partial class Chat : ComponentBase
{
    [SupplyParameterFromQuery(Name = "channelId")]
    public string? ChannelId { get; set; }

    private Dictionary<string, object>? _parameters;

    protected override void OnParametersSet()
    {
        _parameters = string.IsNullOrEmpty(ChannelId)
            ? null
            : new Dictionary<string, object> { ["ChannelId"] = ChannelId };
    }
}
