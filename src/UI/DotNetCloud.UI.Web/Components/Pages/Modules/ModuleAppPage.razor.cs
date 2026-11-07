using DotNetCloud.Core.Modules.Ui;
using DotNetCloud.UI.Web.Components.Modules;
using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Components.Pages.Modules;

/// <summary>
/// Core-chrome shell page for a module that serves its own UI.
/// </summary>
/// <remarks>
/// The page itself lives at <c>/modules/{moduleId}</c> so the sidebar and header stay around the
/// framed app; the iframe loads <c>/apps/{moduleId}/**</c>, which the core proxies to the module
/// process. Splitting the two paths is what keeps the browser URL (core) and the framed document
/// (module) from fighting over the same route.
/// </remarks>
public partial class ModuleAppPage : ComponentBase
{
    private ModuleAppDescriptor? _app;
    private string _path = "/";
    private string _pageTitle = "App";

    /// <summary>Gets or sets the module id from the route.</summary>
    [Parameter]
    public string? ModuleId { get; set; }

    /// <summary>Gets or sets the optional module-relative remainder of the route.</summary>
    [Parameter]
    public string? CatchAll { get; set; }

    [Inject]
    private ModuleUiRegistry ModuleRegistry { get; set; } = default!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _path = ModuleAppUrls.NormalizeEntryPath(string.IsNullOrEmpty(CatchAll) ? "/" : "/" + CatchAll);
        _app = string.IsNullOrEmpty(ModuleId) ? null : ModuleRegistry.GetApp(ModuleId);
        _pageTitle = _app?.Title ?? "App";
    }
}
