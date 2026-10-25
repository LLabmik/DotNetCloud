using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Forms;

/// <summary>Code-behind for the <see cref="DncFormValidation"/> component.</summary>
public partial class DncFormValidation : ComponentBase
{
    /// <summary>Additional CSS class for the container.</summary>
    [Parameter]
    public string? CssClass { get; set; }
}
