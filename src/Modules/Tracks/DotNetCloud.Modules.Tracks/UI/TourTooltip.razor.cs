using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Code-behind for the tour tooltip component.
/// Renders a centered tooltip card with emoji, title, description, and navigation buttons.
/// </summary>
public class TourTooltipBase : ComponentBase
{
    [Parameter] public string? Emoji { get; set; }

    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public string? Description { get; set; }

    [Parameter]
    public int CurrentStep { get; set; }

    [Parameter]
    public int TotalSteps { get; set; }

    [Parameter]
    public EventCallback OnPrevious { get; set; }

    [Parameter]
    public EventCallback OnNext { get; set; }

    [Parameter]
    public EventCallback OnSkip { get; set; }
}
