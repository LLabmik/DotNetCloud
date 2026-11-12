using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.Web.Pages;

/// <summary>
/// Code-behind for the module UI's entry page. The deep-link query parameters are bound by the
/// router (<see cref="SupplyParameterFromQueryAttribute"/>) and forwarded to the Tracks shell.
/// </summary>
public partial class Tracks
{
    /// <summary>Gets or sets the work item whose kanban board should open.</summary>
    [SupplyParameterFromQuery(Name = "kanban")]
    public string? Kanban { get; set; }

    /// <summary>Gets or sets the id of the work item whose detail panel should open.</summary>
    [SupplyParameterFromQuery(Name = "card")]
    public string? Card { get; set; }
}
