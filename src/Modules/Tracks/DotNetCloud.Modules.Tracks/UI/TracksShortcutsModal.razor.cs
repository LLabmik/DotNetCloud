using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>Code-behind for the <see cref="TracksShortcutsModal"/> component.</summary>
public partial class TracksShortcutsModal : ComponentBase
{
    [Parameter] public EventCallback OnClose { get; set; }
}
