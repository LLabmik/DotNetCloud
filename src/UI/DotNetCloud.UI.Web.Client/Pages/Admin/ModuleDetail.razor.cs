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

/// <summary>Code-behind for the <see cref="ModuleDetail"/> component.</summary>
public partial class ModuleDetail : ComponentBase
{
    [Parameter] public string ModuleId { get; set; } = string.Empty;
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private bool _loading = true;
    private bool _actionInProgress;
    private ModuleDto? _module;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _module = await Api.GetModuleAsync(ModuleId);
        }
        catch
        {
            Toast.ShowError("Failed to load module details.");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task StartAsync()
    {
        _actionInProgress = true;
        if (await Api.StartModuleAsync(ModuleId))
        {
            Toast.ShowSuccess("Module started.");
            await LoadAsync();
        }
        else Toast.ShowError("Failed to start module.");
        _actionInProgress = false;
    }

    private async Task StopAsync()
    {
        _actionInProgress = true;
        if (await Api.StopModuleAsync(ModuleId))
        {
            Toast.ShowSuccess("Module stopped.");
            await LoadAsync();
        }
        else Toast.ShowError("Failed to stop module.");
        _actionInProgress = false;
    }

    private async Task RestartAsync()
    {
        _actionInProgress = true;
        if (await Api.RestartModuleAsync(ModuleId))
        {
            Toast.ShowSuccess("Module restarted.");
            await LoadAsync();
        }
        else Toast.ShowError("Failed to restart module.");
        _actionInProgress = false;
    }

    private static string GetStatusClass(string status) => status switch
    {
        "Enabled" => "success",
        "Disabled" => "muted",
        _ => "warning"
    };
}
