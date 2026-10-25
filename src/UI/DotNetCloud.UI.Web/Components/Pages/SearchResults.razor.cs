using System.Net.Http;
using System.Security.Claims;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.DTOs.Search;
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

namespace DotNetCloud.UI.Web.Components.Pages;

/// <summary>Code-behind for the <see cref="SearchResults"/> component.</summary>
public partial class SearchResults : ComponentBase
{
    [SupplyParameterFromQuery(Name = "q")]
    public string? QueryParam { get; set; }

    [SupplyParameterFromQuery(Name = "module")]
    public string? ModuleParam { get; set; }

    [SupplyParameterFromQuery(Name = "page")]
    public int? PageParam { get; set; }

    private string _query = string.Empty;
    private string _lastQuery = string.Empty;
    private string? _moduleFilter;
    private string _sortOrder = "relevance";
    private int _page = 1;
    private int _pageSize = 20;
    private bool _isLoading;
    private bool _hasSearched;
    private SearchResultDto? _result;
    private CancellationTokenSource? _debounceCts;
    private bool? _isMusicModuleInstalled;

    private int TotalPages => _result is null ? 1 : Math.Max(1, (int)Math.Ceiling((double)_result.TotalCount / _pageSize));

    protected override async Task OnInitializedAsync()
    {
        _query = QueryParam ?? string.Empty;
        _moduleFilter = ModuleParam;
        _page = PageParam ?? 1;

        if (!string.IsNullOrWhiteSpace(_query))
        {
            await ExecuteSearch();
        }
    }

    private async Task OnQueryChanged()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        if (string.IsNullOrWhiteSpace(_query))
        {
            _result = null;
            _hasSearched = false;
            return;
        }

        try
        {
            await Task.Delay(400, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        _page = 1;
        await ExecuteSearch();
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrWhiteSpace(_query))
        {
            _debounceCts?.Cancel();
            _page = 1;
            _ = ExecuteSearch();
        }
    }

    private async Task ExecuteSearch()
    {
        if (string.IsNullOrWhiteSpace(_query)) return;

        _isLoading = true;
        _lastQuery = _query;
        StateHasChanged();

        try
        {
            _result = await Api.SearchAsync(
                _query,
                _moduleFilter,
                page: _page,
                pageSize: _pageSize,
                sort: _sortOrder);
            _hasSearched = true;
            UpdateUrl();
        }
        catch
        {
            _result = null;
            _hasSearched = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ClearQuery()
    {
        _query = string.Empty;
        _result = null;
        _hasSearched = false;
    }

    private async Task SetModuleFilter(string? module)
    {
        _moduleFilter = module;
        _page = 1;
        await ExecuteSearch();
    }

    private async Task PreviousPage()
    {
        if (_page > 1)
        {
            _page--;
            await ExecuteSearch();
        }
    }

    private async Task NextPage()
    {
        if (_page < TotalPages)
        {
            _page++;
            await ExecuteSearch();
        }
    }

    private void UpdateUrl()
    {
        var url = $"/search?q={Uri.EscapeDataString(_query)}";
        if (!string.IsNullOrWhiteSpace(_moduleFilter))
            url += $"&module={Uri.EscapeDataString(_moduleFilter)}";
        if (_page > 1)
            url += $"&page={_page}";
        Navigation.NavigateTo(url, replace: true);
    }

    private async Task NavigateToResultAsync(SearchResultItem item)
    {
        var url = await GetResultUrlAsync(item);
        Navigation.NavigateTo(url);
    }

    private async Task<string> GetResultUrlAsync(SearchResultItem item)
    {
        var openMusicModule = false;

        if (SearchResultNavigationHelper.IsMusicFileSearchResult(item))
        {
            var isMusicModuleInstalled = await IsMusicModuleInstalledAsync();
            if (isMusicModuleInstalled)
            {
                openMusicModule = await JS.InvokeAsync<bool>("confirm",
                    "This looks like a music file. Open in Music module?\n\nOK = Music, Cancel = Files");
            }
        }

        var nav = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return SearchResultNavigationHelper.GetResultUrl(item, nav, openMusicModule);
    }

    private async Task<bool> IsMusicModuleInstalledAsync()
    {
        if (_isMusicModuleInstalled.HasValue)
        {
            return _isMusicModuleInstalled.Value;
        }

        try
        {
            _isMusicModuleInstalled = await Api.IsModuleAvailableAsync("dotnetcloud.music");
        }
        catch
        {
            _isMusicModuleInstalled = false;
        }

        return _isMusicModuleInstalled.Value;
    }
}
