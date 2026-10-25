using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Shared.Components.Navigation;

/// <summary>Code-behind for the <see cref="ModuleHelpLink"/> component.</summary>
public partial class ModuleHelpLink : ComponentBase
{
    /// <summary>The module identifier used in the URL path (e.g., "files", "notes").</summary>
    [Parameter, EditorRequired]
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Whether the parent sidebar is collapsed (icon-only mode).</summary>
    [Parameter]
    public bool Collapsed { get; set; }

    private void NavigateToHelp()
    {
        Navigation.NavigateTo($"/apps/{ModuleId}/help");
    }
}
