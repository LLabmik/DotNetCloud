using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Code-behind for the Tracks command palette (Ctrl+K).
/// Provides keyboard-driven navigation, fuzzy search, and quick actions.
/// </summary>
public class TracksCommandPaletteBase : ComponentBase, IDisposable
{
    [Inject] protected ICommandPaletteService CommandPaletteService { get; set; } = null!;
    [Inject] protected IJSRuntime JsRuntime { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;

    [Parameter] public Guid OrganizationId { get; set; }
    [Parameter] public Guid? CurrentProductId { get; set; }
    [Parameter] public EventCallback<PaletteItem> OnItemSelected { get; set; }

    protected bool _isVisible;
    protected bool _isLoading;
    protected string _query = string.Empty;
    protected int _selectedIndex = -1;

    protected List<(string Group, List<PaletteItem> Items)> _results = [];
    protected List<PaletteItem>? _groupsFlat;
    protected ElementReference _searchInput;

    private CancellationTokenSource? _searchCts;
    private DotNetObjectReference<TracksCommandPaletteBase>? _jsRef;
    private readonly List<Guid> _recentWorkItemIds = [];

    private const int MaxRecentItems = 10;

    protected override void OnInitialized()
    {
        _jsRef = DotNetObjectReference.Create(this);
        LoadRecentItems();
    }

    /// <summary>
    /// Opens the command palette.
    /// </summary>
    public async Task OpenAsync()
    {
        _isVisible = true;
        _query = string.Empty;
        _selectedIndex = -1;
        _results = [];
        _groupsFlat = null;

        StateHasChanged();

        // Load initial results (recent items + quick actions)
        await LoadInitialResultsAsync();

        // Focus the search input after render
        await Task.Delay(50);
        await _searchInput.FocusAsync();
    }

    /// <summary>
    /// Closes the command palette.
    /// </summary>
    public void Close()
    {
        _isVisible = false;
        _searchCts?.Cancel();
        StateHasChanged();
    }

    private async Task LoadInitialResultsAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var result = await CommandPaletteService.SearchAsync(OrganizationId, _query, CurrentProductId, CancellationToken.None);
            BuildResults(result);
        }
        catch
        {
            _results = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task PerformSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        _isLoading = true;
        StateHasChanged();

        try
        {
            // Debounce
            await Task.Delay(150, ct);

            if (ct.IsCancellationRequested) return;

            var result = await CommandPaletteService.SearchAsync(OrganizationId, _query, CurrentProductId, ct);
            if (ct.IsCancellationRequested) return;

            BuildResults(result);
        }
        catch (OperationCanceledException)
        {
            // Expected on debounce cancellation
        }
        catch (Exception)
        {
            _results = [];
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                _isLoading = false;
            }
        }
    }

    private void BuildResults(CommandPaletteResult result)
    {
        var list = new List<(string Group, List<PaletteItem> Items)>();

        // Recent items first if no query
        if (string.IsNullOrWhiteSpace(_query) && _recentWorkItemIds.Count > 0)
        {
            var recentItems = result.WorkItems
                .Where(wi => _recentWorkItemIds.Contains(Guid.Parse(wi.Id)))
                .ToList();

            if (recentItems.Count > 0)
            {
                list.Add(("Recent", recentItems));
            }
        }

        if (result.WorkItems.Count > 0)
            list.Add(("Work Items", result.WorkItems));
        if (result.Products.Count > 0)
            list.Add(("Products", result.Products));
        if (result.Sprints.Count > 0)
            list.Add(("Sprints", result.Sprints));
        if (result.Views.Count > 0)
            list.Add(("Views", result.Views));
        if (result.Actions.Count > 0)
            list.Add(("Actions", result.Actions));

        _results = list;

        // Flatten for keyboard navigation
        _groupsFlat = list.SelectMany(g => g.Items).ToList();
        if (_groupsFlat.Count > 0)
            _selectedIndex = 0;
        else
            _selectedIndex = -1;
    }

    protected async Task HandleKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Escape":
                Close();
                break;

            case "ArrowDown":
                if (_groupsFlat is not null && _groupsFlat.Count > 0)
                {
                    _selectedIndex = (_selectedIndex + 1) % _groupsFlat.Count;
                    await ScrollSelectedIntoViewAsync();
                }
                break;

            case "ArrowUp":
                if (_groupsFlat is not null && _groupsFlat.Count > 0)
                {
                    _selectedIndex = (_selectedIndex - 1 + _groupsFlat.Count) % _groupsFlat.Count;
                    await ScrollSelectedIntoViewAsync();
                }
                break;

            case "Enter":
                if (_groupsFlat is not null && _selectedIndex >= 0 && _selectedIndex < _groupsFlat.Count)
                {
                    var item = _groupsFlat[_selectedIndex];
                    await ExecuteItem(item);
                }
                break;

            default:
                // Trigger search on any text input
                if (!string.IsNullOrEmpty(e.Key) && e.Key.Length == 1)
                {
                    _ = PerformSearchAsync();
                }
                break;
        }
    }

    protected async Task HandleOverlayKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            Close();
            return;
        }
        await Task.CompletedTask;
    }

    protected async Task ExecuteItem(PaletteItem item)
    {
        TrackRecentItem(item);

        switch (item.Action)
        {
            case "navigate":
                Close();
                Navigation.NavigateTo(item.ActionUrl);
                break;

            case "new-epic":
            case "new-item":
            case "my-items":
            case "settings":
            case "shortcuts":
            case "toggle-dark-mode":
                await OnItemSelected.InvokeAsync(item);
                Close();
                break;

            default:
                Close();
                break;
        }
    }

    protected static string GetIcon(PaletteItem item)
    {
        return item.Subtitle switch
        {
            "Quick Action" => "⚡",
            "Product" => "📦",
            "Sprint" => "🏃",
            "Saved View" => "👁",
            "Epic" => "⚡",
            "Feature" => "🔷",
            "Item" => "📋",
            "SubItem" => "📌",
            _ => "📋"
        };
    }

    private void TrackRecentItem(PaletteItem item)
    {
        if (!Guid.TryParse(item.Id, out var guid)) return;

        _recentWorkItemIds.Remove(guid);
        _recentWorkItemIds.Insert(0, guid);

        if (_recentWorkItemIds.Count > MaxRecentItems)
            _recentWorkItemIds.RemoveRange(MaxRecentItems, _recentWorkItemIds.Count - MaxRecentItems);

        PersistRecentItems();
    }

    private void LoadRecentItems()
    {
        _recentWorkItemIds.Clear();
        // Recent items are stored in localStorage via JS interop
    }

    private void PersistRecentItems()
    {
        // Persist to localStorage via JS interop
        _ = JsRuntime.InvokeVoidAsync("localStorage.setItem",
            "tracks_command_palette_recents",
            string.Join(",", _recentWorkItemIds));
    }

    private async Task ScrollSelectedIntoViewAsync()
    {
        await JsRuntime.InvokeVoidAsync("eval",
            "document.querySelector('.cp-item-selected')?.scrollIntoView({ block: 'nearest' })");
    }

    protected int FindFlatIndex(PaletteItem item)
    {
        if (_groupsFlat is null) return -1;
        return _groupsFlat.IndexOf(item);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _jsRef?.Dispose();
    }
}
