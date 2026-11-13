using DotNetCloud.Core.DTOs;
using DotNetCloud.UI.Shared.Services;
using DotNetCloud.UI.Web.Client.Services;
using DotNetCloud.UI.Web.Client.Shared;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.UI.Web.Client.Pages.Admin;

/// <summary>Code-behind for the <see cref="ErasureQueue"/> page.</summary>
public partial class ErasureQueue : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private ConfirmDialog _confirmDialog = default!;

    private bool _loading = true;
    private bool _actionInProgress;
    private IReadOnlyList<UserErasureRequestDto> _requests = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _requests = await Api.ListErasureRequestsAsync(includeCompleted: false);
        }
        catch
        {
            Toast.ShowError("Failed to load erasure requests.");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task OverrideAsync(Guid requestId)
    {
        if (!await _confirmDialog.ShowAsync(
                "Force-complete this request? Modules that never acknowledged will not have erased the data.",
                "Force complete"))
        {
            return;
        }

        _actionInProgress = true;
        try
        {
            if (await Api.OverrideErasureAsync(requestId))
            {
                Toast.ShowSuccess("Erasure request force-completed.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError("Failed to override the erasure request.");
            }
        }
        finally
        {
            _actionInProgress = false;
        }
    }

    private static string GetStatusClass(string status) => status switch
    {
        "Completed" or "Acknowledged" => "success",
        "Failed" or "Error" => "danger",
        _ => "warning"
    };
}
