using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Chat.UI;

/// <summary>
/// Code-behind for the channel header component.
/// </summary>
public partial class ChannelHeader : ComponentBase
{
    /// <summary>The currently selected channel.</summary>
    [Parameter]
    public ChannelViewModel? Channel { get; set; }

    /// <summary>Callback to toggle the member list panel.</summary>
    [Parameter]
    public EventCallback OnToggleMemberList { get; set; }

    /// <summary>Callback to open search.</summary>
    [Parameter]
    public EventCallback OnSearch { get; set; }

    /// <summary>Toggles the member list panel.</summary>
    protected async Task ToggleMemberList()
    {
        await OnToggleMemberList.InvokeAsync();
    }

    /// <summary>Handles search button click.</summary>
    protected async Task OnSearchClick()
    {
        await OnSearch.InvokeAsync();
    }
}
