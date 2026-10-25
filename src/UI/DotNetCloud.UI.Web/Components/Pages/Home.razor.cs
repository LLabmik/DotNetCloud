using System.Net.Http;
using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Localization;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Editors;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Layout;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Resources;
using DotNetCloud.UI.Shared.Services;
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Client.Shared;
using DotNetCloud.UI.Web.Components.Home;
using DotNetCloud.UI.Web.Components.Layout;
using DotNetCloud.UI.Web.Components.Pages;
using DotNetCloud.UI.Web.Components.Pages.Modules.Help;
using DotNetCloud.UI.Web.Components.Shared;
using DotNetCloud.UI.Web.Components.Shared.Search;
using DotNetCloud.UI.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Components.Pages;

/// <summary>Code-behind for the <see cref="Home"/> component.</summary>
public partial class Home : ComponentBase, IDisposable
{
    [Inject] private ModuleUiRegistry ModuleRegistry { get; set; } = default!;
    [Inject] private WidgetUiRegistry WidgetRegistry { get; set; } = default!;
    [Inject] private HomeWidgetLayoutService LayoutService { get; set; } = default!;

    private bool _customizing;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        WidgetRegistry.OnChange += OnWidgetsChanged;
        LayoutService.OnChange += OnLayoutChanged;
        await LayoutService.EnsureLoadedAsync();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        WidgetRegistry.OnChange -= OnWidgetsChanged;
        LayoutService.OnChange -= OnLayoutChanged;
    }

    private void OnWidgetsChanged()
    {
        // A module was installed or removed - re-resolve so new widgets appear in the user's order.
        LayoutService.RefreshFromRegistry();
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnLayoutChanged()
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private void ToggleCustomize()
    {
        _customizing = !_customizing;
    }

    private void CloseCustomize()
    {
        LayoutService.ClearError();
        _customizing = false;
    }

    private int _widgetRefreshToken;

    /// <summary>
    /// Force-refreshes widget data by bumping the remount key, which recreates each widget
    /// component so its <c>OnInitializedAsync</c> re-runs and reloads fresh data.
    /// </summary>
    private void RefreshWidgets()
    {
        _widgetRefreshToken++;
    }
}
