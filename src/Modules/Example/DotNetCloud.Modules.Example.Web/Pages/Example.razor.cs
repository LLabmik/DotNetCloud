using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Example.Web.Pages;

/// <summary>
/// Code-behind for the module UI's entry page.
/// </summary>
/// <remarks>
/// The view is driven by the <c>view</c> query parameter rather than a route segment: the module proxy
/// only serves static files under <c>/apps/{moduleId}/**</c>, so a path the WebAssembly router owns
/// would 404 on reload.
/// </remarks>
public partial class Example
{
    /// <summary>Gets or sets the requested view (<c>notes</c> or <c>help</c>).</summary>
    [SupplyParameterFromQuery(Name = "view")]
    public string? View { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    private bool IsHelp => string.Equals(View, "help", StringComparison.OrdinalIgnoreCase);

    private async Task ShowAsync(bool help)
    {
        if (IsHelp == help)
        {
            return;
        }

        Navigation.NavigateTo(help ? "?view=help" : "?view=notes");
        await Task.CompletedTask;
    }
}
