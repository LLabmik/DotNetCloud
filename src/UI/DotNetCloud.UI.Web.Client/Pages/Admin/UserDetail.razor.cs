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

/// <summary>Code-behind for the <see cref="UserDetail"/> component.</summary>
public partial class UserDetail : ComponentBase
{
    [Parameter] public Guid UserId { get; set; }
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _loading = true;
    private bool _actionInProgress;
    private bool _showResetPassword;
    private string _newPassword = string.Empty;
    private UserDto? _user;
    private QuotaResponse? _quota;
    private ConfirmDialog _confirmDialog = default!;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _user = await Api.GetUserAsync(UserId);
            _quota = await Api.GetUserQuotaAsync(UserId);
        }
        catch { Toast.ShowError("Failed to load user."); }
        finally { _loading = false; }
    }

    private async Task DisableAsync()
    {
        if (!await _confirmDialog.ShowAsync("Are you sure you want to disable this user?", "Disable User"))
            return;
        _actionInProgress = true;
        if (await Api.DisableUserAsync(UserId))
        {
            Toast.ShowSuccess("User disabled.");
            await LoadAsync();
        }
        else Toast.ShowError("Failed to disable user.");
        _actionInProgress = false;
    }

    private async Task EnableAsync()
    {
        _actionInProgress = true;
        if (await Api.EnableUserAsync(UserId))
        {
            Toast.ShowSuccess("User enabled.");
            await LoadAsync();
        }
        else Toast.ShowError("Failed to enable user.");
        _actionInProgress = false;
    }

    private async Task DeleteAsync()
    {
        if (!await _confirmDialog.ShowAsync("This action cannot be undone. Delete this user?", "Delete User"))
            return;
        _actionInProgress = true;
        if (await Api.DeleteUserAsync(UserId))
        {
            Toast.ShowSuccess("User deleted.");
            Nav.NavigateTo("/admin/users");
        }
        else Toast.ShowError("Failed to delete user.");
        _actionInProgress = false;
    }

    private void ShowResetPassword() => _showResetPassword = true;

    private async Task ResetPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(_newPassword))
        {
            Toast.ShowWarning("Password is required.");
            return;
        }
        _actionInProgress = true;
        if (await Api.AdminResetPasswordAsync(UserId, _newPassword))
        {
            Toast.ShowSuccess("Password reset successfully.");
            _showResetPassword = false;
            _newPassword = string.Empty;
        }
        else Toast.ShowError("Failed to reset password.");
        _actionInProgress = false;
    }

    private async Task GrantAdminAsync()
    {
        _actionInProgress = true;
        try
        {
            var roles = new List<string>(_user!.Roles) { "Administrator" };
            var result = await Api.SetUserRolesAsync(UserId, roles);
            if (result is not null)
            {
                Toast.ShowSuccess("Administrator role granted.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError("Failed to grant administrator role.");
            }
        }
        catch { Toast.ShowError("Failed to grant administrator role."); }
        finally { _actionInProgress = false; }
    }

    private async Task RevokeAdminAsync()
    {
        _actionInProgress = true;
        try
        {
            var success = await Api.RemoveUserRoleAsync(UserId, "Administrator");
            if (success)
            {
                Toast.ShowSuccess("Administrator role revoked.");
                await LoadAsync();
            }
            else
            {
                Toast.ShowError("Failed to revoke administrator role.");
            }
        }
        catch { Toast.ShowError("Failed to revoke administrator role."); }
        finally { _actionInProgress = false; }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var i = (int)Math.Floor(Math.Log(Math.Abs(bytes), 1024));
        i = Math.Min(i, units.Length - 1);
        return $"{bytes / Math.Pow(1024, i):F1} {units[i]}";
    }

    private string QuotaBarClass() => _quota?.UsagePercent switch
    {
        >= 95 => "quota-critical",
        >= 80 => "quota-warning",
        _ => ""
    };
}
