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

/// <summary>Code-behind for the <see cref="UserList"/> component.</summary>
public partial class UserList : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;

    private bool _loading = true;
    private int _page = 1;
    private string _search = string.Empty;
    private PaginatedResult<UserDto> _result = new();
    private Dictionary<Guid, QuotaResponse> _quotas = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        _loading = true;
        try
        {
            var usersTask = Api.ListUsersAsync(
                page: _page,
                pageSize: 25,
                search: string.IsNullOrWhiteSpace(_search) ? null : _search);
            var quotasTask = Api.GetAllQuotasAsync();
            await Task.WhenAll(usersTask, quotasTask);
            _result = usersTask.Result;
            _quotas = quotasTask.Result;
        }
        catch (Exception ex)
        {
            Toast.ShowError($"Failed to load users. {ex.Message}");
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SearchAsync()
    {
        _page = 1;
        await LoadUsersAsync();
    }

    private async Task PreviousPage()
    {
        if (_page > 1)
        {
            _page--;
            await LoadUsersAsync();
        }
    }

    private async Task NextPage()
    {
        if (_page < _result.TotalPages)
        {
            _page++;
            await LoadUsersAsync();
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        if (bytes >= 1024L * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        if (bytes >= 1024L) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    private static string QuotaBarClass(QuotaResponse q)
    {
        if (q.UsagePercent >= 95) return "quota-critical";
        if (q.UsagePercent >= 80) return "quota-warning";
        return "";
    }
}
