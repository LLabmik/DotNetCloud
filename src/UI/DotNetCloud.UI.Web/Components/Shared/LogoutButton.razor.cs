using DotNetCloud.UI.Shared.Components.Dialogs;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetCloud.UI.Web.Components.Shared;

/// <summary>Code-behind for the <see cref="LogoutButton"/> component.</summary>
public partial class LogoutButton : ComponentBase
{
    private DncConfirmDialog? _confirmDialog;

    private async Task PromptLogoutAsync()
    {
        var confirmed = await _confirmDialog!.ShowAsync(
            "Are you sure you want to sign out? Any unsaved changes will be lost.",
            "Sign Out");
        if (confirmed)
        {
            await Js.InvokeVoidAsync("DotNetCloudLogout.submitLogoutForm");
        }
    }
}
