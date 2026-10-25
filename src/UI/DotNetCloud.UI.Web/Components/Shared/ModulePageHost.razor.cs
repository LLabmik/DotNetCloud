using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>Code-behind for the <see cref="ModulePageHost"/> component.</summary>
public partial class ModulePageHost : ComponentBase, IDisposable
{
    /// <summary>
    /// The route key identifying the module page to render (e.g., "files.browser").
    /// </summary>
    [Parameter, EditorRequired]
    public string RouteKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional parameters to pass to the dynamic component.
    /// </summary>
    [Parameter]
    public IDictionary<string, object>? Parameters { get; set; }

    private Type? _componentType;
    private ErrorBoundary? _moduleErrorBoundary;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += OnLocationChanged;
    }

    protected override void OnParametersSet()
    {
        _componentType = ModuleRegistry.GetPage(RouteKey);
    }

    private void RecoverModule() => _moduleErrorBoundary?.Recover();

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _moduleErrorBoundary?.Recover();
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
