using DotNetCloud.Core.DTOs;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Services;
using DotNetCloud.UI.Web.Client.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Client.Pages;

/// <summary>
/// Code-behind for the "Connected apps" page: the modules the signed-in user has approved, with a
/// revoke action per module.
/// </summary>
public partial class ConnectedApps : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;
    [Inject] private BrowserTimeProvider TimeProvider { get; set; } = default!;

    private bool _loading = true;
    private string _busyModuleId = string.Empty;
    private IReadOnlyList<ConnectedAppDto> _apps = [];

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await TimeProvider.EnsureInitializedAsync();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _apps = await Api.GetConnectedAppsAsync();
        }
        catch
        {
            Toast.ShowError("Failed to load connected applications.");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task RevokeAsync(ConnectedAppDto app)
    {
        _busyModuleId = app.ModuleId;

        try
        {
            if (await Api.RevokeConsentAsync(app.ModuleId))
            {
                Toast.ShowSuccess($"Access revoked for {app.ModuleName}.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError("Failed to revoke access.");
            }
        }
        finally
        {
            _busyModuleId = string.Empty;
        }
    }
}
