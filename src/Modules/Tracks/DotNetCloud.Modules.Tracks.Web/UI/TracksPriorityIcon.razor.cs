using DotNetCloud.Core.DTOs;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.Web.UI;

/// <summary>
/// Code-behind for <see cref="TracksPriorityIcon"/> — renders a priority as a coloured Material dot.
/// </summary>
public partial class TracksPriorityIcon : ComponentBase
{
    /// <summary>The priority to render.</summary>
    [Parameter]
    [EditorRequired]
    public Priority Priority { get; set; }

    /// <summary>Tooltip text. Defaults to the priority name.</summary>
    [Parameter]
    public string? Title { get; set; }
}
