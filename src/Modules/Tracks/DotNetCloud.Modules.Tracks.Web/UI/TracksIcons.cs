using DotNetCloud.Core.DTOs;

namespace DotNetCloud.Modules.Tracks.Web.UI;

/// <summary>
/// Single source of truth for the Material icon names the Tracks UI uses for its work-item,
/// priority, view and palette vocabulary.
/// </summary>
/// <remarks>
/// Components render the returned names through <c>MaterialIcon</c>, so no Tracks markup carries
/// emoji, text glyphs or third-party SVG. Priority is the exception the UI shows as a colour, and
/// the colour lives in CSS (<c>tracks-priority--*</c>) rather than in the icon name.
/// </remarks>
public static class TracksIcons
{
    /// <summary>Icon used when a work item type is unknown.</summary>
    public const string WorkItem = "checklist";

    /// <summary>Icon for a work-item type.</summary>
    /// <param name="type">The work-item type.</param>
    /// <returns>A Material icon name.</returns>
    public static string ForType(WorkItemType type) => type switch
    {
        WorkItemType.Epic => "build",
        WorkItemType.Feature => "star",
        WorkItemType.SubItem => "bookmark",
        _ => WorkItem,
    };

    /// <summary>Icon for a priority. Every priority renders the same dot; the colour comes from CSS.</summary>
    public const string PriorityDot = "circle";

    /// <summary>CSS modifier suffix for a priority colour (<c>tracks-priority--urgent</c>).</summary>
    /// <param name="priority">The priority.</param>
    /// <returns>A lower-case token, or <c>none</c>.</returns>
    public static string PriorityToken(Priority priority) => priority switch
    {
        Priority.Urgent => "urgent",
        Priority.High => "high",
        Priority.Medium => "medium",
        Priority.Low => "low",
        _ => "none",
    };

    /// <summary>Icon for a saved-view kind.</summary>
    /// <param name="kind">The view kind (e.g. <c>Kanban</c>).</param>
    /// <returns>A Material icon name.</returns>
    public static string ForViewKind(string? kind) => kind switch
    {
        "Kanban" => "checklist",
        "Backlog" => "edit_note",
        "List" => "view_list",
        "Calendar" => "calendar_today",
        "Timeline" => "trending_up",
        _ => "search",
    };

    /// <summary>Icon for a command-palette category.</summary>
    /// <param name="category">The category shown next to the result.</param>
    /// <returns>A Material icon name.</returns>
    public static string ForPaletteCategory(string? category) => category switch
    {
        "Quick Action" => "speed",
        "Product" => "inventory_2",
        "Sprint" => "directions_run",
        "Saved View" => "bookmark",
        "Epic" => "build",
        "Feature" => "star",
        "SubItem" => "bookmark",
        _ => WorkItem,
    };
}
