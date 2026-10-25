using System.Net.Http;
using System.Net.Http.Json;
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace DotNetCloud.UI.Web.Client.Pages.Admin;

/// <summary>Code-behind for the <see cref="ModuleList"/> component.</summary>
public partial class ModuleList : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private bool _loading = true;
    private bool _actionInProgress;
    private string? _loadError;
    private IReadOnlyList<ModuleDto> _modules = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadModulesAsync();
    }

    private async Task LoadModulesAsync()
    {
        _loading = true;
        _loadError = null;
        try
        {
            _modules = await Api.ListModulesAsync();
        }
        catch (Exception ex)
        {
            _loadError = "Failed to load modules.";
            Toast.ShowError($"Failed to load modules. {ex.Message}");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task StartModuleAsync(string moduleId)
    {
        _actionInProgress = true;
        try
        {
            if (await Api.StartModuleAsync(moduleId))
            {
                Toast.ShowSuccess($"Module '{moduleId}' started.");
                await LoadModulesAsync();
            }
            else
            {
                Toast.ShowError($"Failed to start module '{moduleId}'.");
            }
        }
        finally { _actionInProgress = false; }
    }

    private async Task StopModuleAsync(string moduleId)
    {
        _actionInProgress = true;
        try
        {
            if (await Api.StopModuleAsync(moduleId))
            {
                Toast.ShowSuccess($"Module '{moduleId}' stopped.");
                await LoadModulesAsync();
            }
            else
            {
                Toast.ShowError($"Failed to stop module '{moduleId}'.");
            }
        }
        finally { _actionInProgress = false; }
    }

    private async Task RestartModuleAsync(string moduleId)
    {
        _actionInProgress = true;
        try
        {
            if (await Api.RestartModuleAsync(moduleId))
            {
                Toast.ShowSuccess($"Module '{moduleId}' restarted.");
                await LoadModulesAsync();
            }
            else
            {
                Toast.ShowError($"Failed to restart module '{moduleId}'.");
            }
        }
        finally { _actionInProgress = false; }
    }

    private static string GetStatusClass(string status) => status switch
    {
        "Enabled" => "success",
        "Disabled" => "muted",
        "UpdateAvailable" => "warning",
        _ => "muted"
    };
}
