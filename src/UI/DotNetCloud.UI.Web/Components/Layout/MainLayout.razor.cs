using System.Net.Http;
using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Localization;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Music.UI;
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
using DotNetCloud.UI.Web.Components.Layout;
using DotNetCloud.UI.Web.Components.Pages;
using DotNetCloud.UI.Web.Components.Pages.Modules.Help;
using DotNetCloud.UI.Web.Components.Shared;
using DotNetCloud.UI.Web.Components.Shared.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Components.Layout;

/// <summary>Code-behind for the <see cref="MainLayout"/> component.</summary>
public partial class MainLayout
{
    private const string UiSettingsModule = "dotnetcloud.ui";
    private const string NavbarCollapsedKey = "navbar.collapsed";

    private bool _collapsed;
    private ErrorBoundary? _errorBoundary;
    private PersistingComponentStateSubscription _persistingSubscription;

    protected override async Task OnInitializedAsync()
    {
        _persistingSubscription = PersistentState.RegisterOnPersisting(PersistState);
        Navigation.LocationChanged += OnLocationChanged;

        if (PersistentState.TryTakeFromJson<bool>(NavbarCollapsedKey, out var restored))
        {
            _collapsed = restored;
        }
        else
        {
            await LoadSidebarPreferenceAsync();
        }
    }

    private Task PersistState()
    {
        PersistentState.PersistAsJson(NavbarCollapsedKey, _collapsed);
        return Task.CompletedTask;
    }

    private async Task ToggleSidebarAsync()
    {
        _collapsed = !_collapsed;
        await SaveSidebarPreferenceAsync();
    }

    private async Task LoadSidebarPreferenceAsync()
    {
        var userId = await GetAuthenticatedUserIdAsync();
        if (userId is null)
        {
            return;
        }

        try
        {
            var setting = await UserSettingsService.GetSettingAsync(userId.Value, UiSettingsModule, NavbarCollapsedKey);
            _collapsed = setting is not null && bool.TryParse(setting.Value, out var isCollapsed) && isCollapsed;
        }
        catch
        {
            _collapsed = false;
        }
    }

    private async Task SaveSidebarPreferenceAsync()
    {
        var userId = await GetAuthenticatedUserIdAsync();
        if (userId is null)
        {
            return;
        }

        try
        {
            await UserSettingsService.UpsertSettingAsync(
                userId.Value,
                UiSettingsModule,
                NavbarCollapsedKey,
                new UpsertUserSettingDto
                {
                    Value = _collapsed.ToString().ToLowerInvariant(),
                    Description = "Whether the user prefers the left navigation collapsed.",
                    IsSensitive = false,
                });
        }
        catch
        {
            // Intentionally ignored so the UI remains responsive if preference persistence fails.
        }
    }

    private async Task<Guid?> GetAuthenticatedUserIdAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        var claimValue = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }

    private void RecoverError() => _errorBoundary?.Recover();

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        _errorBoundary?.Recover();
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        _persistingSubscription.Dispose();
    }
}
