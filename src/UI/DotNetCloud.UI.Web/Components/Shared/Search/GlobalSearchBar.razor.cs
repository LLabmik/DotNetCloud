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

namespace DotNetCloud.UI.Web.Components.Shared.Search;

/// <summary>Code-behind for the <see cref="GlobalSearchBar"/> component.</summary>
public partial class GlobalSearchBar : ComponentBase, IAsyncDisposable
{
    private string _query = string.Empty;
    private bool _isOpen;
    private bool _isLoading;
    private bool _hasSearched;
    private int _selectedIndex = -1;
    private IReadOnlyList<SearchResultItem>? _suggestions;
    private List<string> _recentSearches = [];
    private DotNetObjectReference<GlobalSearchBar>? _dotNetRef;
    private CancellationTokenSource? _debounceCts;
    private bool? _isMusicModuleInstalled;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("DotNetCloudSearch.registerShortcut", _dotNetRef);
        }
    }

    /// <summary>Invoked from JavaScript when Ctrl+K is pressed.</summary>
    [JSInvokable]
    public async Task OnSearchShortcut()
    {
        await OpenSearch();
        StateHasChanged();
    }

    private async Task OpenSearch()
    {
        _isOpen = true;
        _query = string.Empty;
        _suggestions = null;
        _hasSearched = false;
        _selectedIndex = -1;
        await LoadRecentSearches();
        StateHasChanged();
        // Focus the input after render
        await Task.Yield();
        await JS.InvokeVoidAsync("DotNetCloudSearch.focusElement", "#global-search-input");
    }

    private void CloseSearch()
    {
        _isOpen = false;
        _query = string.Empty;
        _suggestions = null;
        _hasSearched = false;
        _selectedIndex = -1;
    }

    private void ClearQuery()
    {
        _query = string.Empty;
        _suggestions = null;
        _hasSearched = false;
        _selectedIndex = -1;
    }

    private async Task OnQueryChanged()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        _selectedIndex = -1;

        if (string.IsNullOrWhiteSpace(_query) || _query.Length < 2)
        {
            _suggestions = null;
            _hasSearched = false;
            return;
        }

        try
        {
            await Task.Delay(300, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        _isLoading = true;
        StateHasChanged();

        try
        {
            _suggestions = await Api.SearchSuggestAsync(_query, token);
            _hasSearched = true;
        }
        catch (TaskCanceledException)
        {
            return;
        }
        catch
        {
            _suggestions = null;
            _hasSearched = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void OnInputKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Escape":
                CloseSearch();
                break;
            case "ArrowDown":
                if (_suggestions is not null && _suggestions.Count > 0)
                    _selectedIndex = Math.Min(_selectedIndex + 1, _suggestions.Count - 1);
                break;
            case "ArrowUp":
                if (_suggestions is not null)
                    _selectedIndex = Math.Max(_selectedIndex - 1, 0);
                break;
            case "Enter":
                if (_suggestions is not null && _selectedIndex >= 0 && _selectedIndex < _suggestions.Count)
                    _ = NavigateToResultAsync(_suggestions[_selectedIndex]);
                else if (!string.IsNullOrWhiteSpace(_query))
                    ViewAllResults();
                break;
        }
    }

    private void OnOverlayKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
            CloseSearch();
    }

    private async Task NavigateToResultAsync(SearchResultItem item)
    {
        SaveRecentSearch(_query);
        CloseSearch();
        var url = await GetResultUrlAsync(item);
        Navigation.NavigateTo(url);
    }

    private void ViewAllResults()
    {
        if (string.IsNullOrWhiteSpace(_query)) return;
        SaveRecentSearch(_query);
        var url = $"/search?q={Uri.EscapeDataString(_query)}";
        CloseSearch();
        Navigation.NavigateTo(url);
    }

    private async Task UseRecentSearch(string query)
    {
        _query = query;
        await OnQueryChanged();
    }

    private async Task LoadRecentSearches()
    {
        try
        {
            var searches = await JS.InvokeAsync<string[]>("DotNetCloudSearch.getRecentSearches");
            _recentSearches = searches?.ToList() ?? [];
        }
        catch
        {
            _recentSearches = [];
        }
    }

    private async void SaveRecentSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        try
        {
            await JS.InvokeVoidAsync("DotNetCloudSearch.addRecentSearch", query);
        }
        catch
        {
            // Ignore localStorage errors
        }
    }

    private async Task ClearRecentSearches()
    {
        _recentSearches.Clear();
        try
        {
            await JS.InvokeVoidAsync("DotNetCloudSearch.clearRecentSearches");
        }
        catch
        {
            // Ignore
        }
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

    /// <summary>Strips HTML highlight tags and decodes HTML entities for plain-text display in the modal.</summary>
    private static string StripHighlight(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var plain = text.Replace("<mark>", "").Replace("</mark>", "");
        return System.Web.HttpUtility.HtmlDecode(plain);
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _dotNetRef?.Dispose();
    }
}
