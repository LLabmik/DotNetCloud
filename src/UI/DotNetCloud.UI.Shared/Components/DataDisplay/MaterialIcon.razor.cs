using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.DataDisplay;

/// <summary>Code-behind for the <see cref="MaterialIcon"/> component.</summary>
public partial class MaterialIcon : ComponentBase
{
    /// <summary>
    /// The Material Icons ligature name (e.g., "home", "settings", "close").
    /// </summary>
    [Parameter, EditorRequired]
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// Icon size variant. Defaults to <see cref="MaterialIconSize.Md"/> (24px).
    /// </summary>
    [Parameter]
    public MaterialIconSize Size { get; set; } = MaterialIconSize.Md;

    /// <summary>
    /// Optional additional CSS classes to append.
    /// </summary>
    [Parameter]
    public string? CssClass { get; set; }

    private string GetSizeClass => Size switch
    {
        MaterialIconSize.Sm => "material-icon--sm",
        MaterialIconSize.Md => "material-icon--md",
        MaterialIconSize.Lg => "material-icon--lg",
        MaterialIconSize.Xl => "material-icon--xl",
        _ => "material-icon--md"
    };
}
