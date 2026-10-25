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

/// <summary>Code-behind for the <see cref="Dashboard"/> component.</summary>
public partial class Dashboard : ComponentBase
{
    [Inject] private DotNetCloudApiClient Api { get; set; } = default!;

    private bool _loading = true;
    private int _userCount;
    private int _orgCount;
    private int _moduleCount;
    private long _totalUsedBytes;
    private long _totalMaxBytes;
    private HealthReportDto? _health;
    private IReadOnlyList<UserDto> _users = [];
    private IReadOnlyList<OrganizationDto> _orgs = [];
    private Dictionary<Guid, QuotaResponse> _quotas = [];
    private List<(UserDto User, QuotaResponse Quota)> _topQuotas = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var usersTask = Api.ListUsersAsync(page: 1, pageSize: 6);
            var orgsTask = Api.ListOrganizationsAsync();
            var modulesTask = Api.ListModulesAsync();
            var quotasTask = Api.GetAllQuotasAsync();
            var healthTask = Api.GetHealthAsync();

            await Task.WhenAll(usersTask, orgsTask, modulesTask, quotasTask, healthTask);

            var usersResult = usersTask.Result;
            _userCount = usersResult.TotalCount;
            _users = usersResult.Items;
            _orgs = orgsTask.Result;
            _orgCount = _orgs.Count;
            _moduleCount = modulesTask.Result.Count;
            _quotas = quotasTask.Result;
            _health = healthTask.Result;

            _totalUsedBytes = _quotas.Values.Sum(q => q.UsedBytes);
            _totalMaxBytes = _quotas.Values.Sum(q => q.MaxBytes);

            // Build top quota users for the panel (users with quotas, sorted by usage)
            _topQuotas = _users
                .Where(u => _quotas.TryGetValue(u.Id, out var q) && q.MaxBytes > 0)
                .Select(u => (u, _quotas[u.Id]))
                .OrderByDescending(x => x.Item2.UsagePercent)
                .Take(5)
                .ToList();
        }
        catch
        {
            // Dashboard is non-critical; show defaults on failure
        }
        finally
        {
            _loading = false;
        }
    }

    private double StoragePercent() =>
        _totalMaxBytes > 0 ? (double)_totalUsedBytes / _totalMaxBytes * 100 : 0;

    private string GetHealthClass() => _health?.Status switch
    {
        "Healthy" => "card-healthy",
        "Degraded" => "card-degraded",
        "Unhealthy" => "card-unhealthy",
        _ => ""
    };

    private string GetHealthDotClass() => _health?.Status switch
    {
        "Healthy" => "dash-health-dot--healthy",
        "Degraded" => "dash-health-dot--degraded",
        "Unhealthy" => "dash-health-dot--unhealthy",
        _ => "dash-health-dot--unknown"
    };

    private static string GetEntryDotClass(string status) => status switch
    {
        "Healthy" => "dash-health-dot--healthy",
        "Degraded" => "dash-health-dot--degraded",
        "Unhealthy" => "dash-health-dot--unhealthy",
        _ => "dash-health-dot--unknown"
    };

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            : name.Length > 0 ? name[0..Math.Min(2, name.Length)].ToUpperInvariant() : "?";
    }

    private static string GetPrimaryRole(UserDto user)
    {
        if (user.Roles.Contains("Admin")) return "Admin";
        if (user.Roles.Contains("Manager")) return "Manager";
        return user.Roles.FirstOrDefault() ?? "User";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
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
