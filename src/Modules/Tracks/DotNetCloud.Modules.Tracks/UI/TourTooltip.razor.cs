using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Code-behind for the tour tooltip component.
/// Renders a positioned tooltip card with title, description, and navigation buttons.
/// </summary>
public class TourTooltipBase : ComponentBase
{
    /// <summary>
    /// The tooltip title (e.g., "Kanban Board").
    /// </summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The tooltip body text describing the feature.
    /// </summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>
    /// Zero-based index of the current step.
    /// </summary>
    [Parameter]
    public int CurrentStep { get; set; }

    /// <summary>
    /// Total number of steps in the tour.
    /// </summary>
    [Parameter]
    public int TotalSteps { get; set; }

    /// <summary>
    /// Position of the tooltip relative to the target element.
    /// </summary>
    [Parameter]
    public TourTooltipPosition Position { get; set; } = TourTooltipPosition.Bottom;

    /// <summary>
    /// Fired when the user clicks "Back".
    /// </summary>
    [Parameter]
    public EventCallback OnPrevious { get; set; }

    /// <summary>
    /// Fired when the user clicks "Next" or "Finish".
    /// </summary>
    [Parameter]
    public EventCallback OnNext { get; set; }

    /// <summary>
    /// Fired when the user clicks "Skip tour".
    /// </summary>
    [Parameter]
    public EventCallback OnSkip { get; set; }
}
