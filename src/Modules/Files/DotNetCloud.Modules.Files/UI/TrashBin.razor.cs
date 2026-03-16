using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the trash bin component.
/// </summary>
public partial class TrashBin : ComponentBase
{
    [Inject] private ITrashService TrashService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    /// <summary>Number of days items are retained before permanent deletion (displayed in the empty state).</summary>
    [Parameter] public int RetentionDays { get; set; } = 30;

    /// <summary>Raised when trash contents change (restore, purge, or empty) so the parent can update counts.</summary>
    [Parameter] public EventCallback OnTrashChanged { get; set; }

    private List<TrashItemViewModel> _trashedItems = [];
    private readonly HashSet<Guid> _selectedItems = [];
    private string _sortColumn = "Date";
    private bool _sortAscending;
    private bool _isLoading;
    private bool _showEmptyConfirm;

    protected override async Task OnInitializedAsync()
    {
        await LoadTrashAsync();
    }

    /// <summary>All trashed items (unsorted).</summary>
    protected IReadOnlyList<TrashItemViewModel> TrashedItems => _trashedItems;

    /// <summary>Trashed items sorted according to the active sort column and direction.</summary>
    protected IReadOnlyList<TrashItemViewModel> SortedItems =>
        (_sortColumn, _sortAscending) switch
        {
            ("Name", true)  => [.. _trashedItems.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)],
            ("Name", false) => [.. _trashedItems.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase)],
            ("Size", true)  => [.. _trashedItems.OrderBy(i => i.Size)],
            ("Size", false) => [.. _trashedItems.OrderByDescending(i => i.Size)],
            ("Date", true)  => [.. _trashedItems.OrderBy(i => i.DeletedAt)],
            _               => [.. _trashedItems.OrderByDescending(i => i.DeletedAt)]
        };

    /// <summary>Number of currently selected items.</summary>
    protected int SelectedCount => _selectedItems.Count;

    /// <summary>Whether all visible items are selected.</summary>
    protected bool IsAllSelected => _trashedItems.Count > 0 && _selectedItems.Count == _trashedItems.Count;

    /// <summary>Human-readable total size of all items in the trash.</summary>
    protected string TrashTotalSizeLabel => FormatSize(_trashedItems.Sum(i => i.Size));

    /// <summary>Whether the loading indicator is shown.</summary>
    protected bool IsLoading => _isLoading;

    /// <summary>Whether the empty-trash confirmation dialog is shown.</summary>
    protected bool IsShowEmptyConfirm => _showEmptyConfirm;

    /// <summary>Returns whether the given item is currently selected.</summary>
    protected bool IsSelected(Guid id) => _selectedItems.Contains(id);

    /// <summary>Toggles selection of a single item.</summary>
    protected void ToggleSelect(Guid id)
    {
        if (!_selectedItems.Add(id)) _selectedItems.Remove(id);
    }

    /// <summary>Selects all items or deselects all, depending on current state.</summary>
    protected void ToggleSelectAll()
    {
        if (IsAllSelected)
            _selectedItems.Clear();
        else
            foreach (var item in _trashedItems) _selectedItems.Add(item.Id);
    }

    /// <summary>Restores selected items back to their original location.</summary>
    protected async Task RestoreSelected()
    {
        var caller = await GetCallerContextAsync();
        foreach (var id in _selectedItems.ToList())
        {
            await TrashService.RestoreAsync(id, caller);
        }

        _selectedItems.Clear();
        await LoadTrashAsync();
        await OnTrashChanged.InvokeAsync();
    }

    /// <summary>Permanently deletes all selected items.</summary>
    protected async Task DeleteSelected()
    {
        var caller = await GetCallerContextAsync();
        foreach (var id in _selectedItems.ToList())
        {
            await TrashService.PermanentDeleteAsync(id, caller);
        }

        _selectedItems.Clear();
        await LoadTrashAsync();
        await OnTrashChanged.InvokeAsync();
    }

    /// <summary>Restores a single item.</summary>
    protected async Task RestoreItem(Guid itemId)
    {
        var caller = await GetCallerContextAsync();
        await TrashService.RestoreAsync(itemId, caller);
        _selectedItems.Remove(itemId);
        await LoadTrashAsync();
        await OnTrashChanged.InvokeAsync();
    }

    /// <summary>Permanently deletes a single item.</summary>
    protected async Task PurgeItem(Guid itemId)
    {
        var caller = await GetCallerContextAsync();
        await TrashService.PermanentDeleteAsync(itemId, caller);
        _selectedItems.Remove(itemId);
        await LoadTrashAsync();
        await OnTrashChanged.InvokeAsync();
    }

    /// <summary>Shows the empty-trash confirmation dialog.</summary>
    protected void ShowEmptyConfirm() => _showEmptyConfirm = true;

    /// <summary>Hides the empty-trash confirmation dialog.</summary>
    protected void HideEmptyConfirm() => _showEmptyConfirm = false;

    /// <summary>Permanently deletes all items in the trash.</summary>
    protected async Task EmptyTrash()
    {
        _showEmptyConfirm = false;
        var caller = await GetCallerContextAsync();
        await TrashService.EmptyTrashAsync(caller);
        _selectedItems.Clear();
        await LoadTrashAsync();
        await OnTrashChanged.InvokeAsync();
    }

    /// <summary>Sets the active sort column; toggles direction if already active.</summary>
    protected void SetSort(string column)
    {
        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = column == "Name";
        }
    }

    /// <summary>Returns the CSS class for a sort header (active/inactive).</summary>
    protected string SortHeaderClass(string column) =>
        _sortColumn == column ? "sort-header--active" : string.Empty;

    /// <summary>Returns the sort direction indicator (▲/▼) for a column header.</summary>
    protected string SortIndicator(string column) =>
        _sortColumn != column ? string.Empty : _sortAscending ? "▲" : "▼";

    /// <summary>Formats a byte count for display (e.g. "3.2 MB").</summary>
    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    private async Task LoadTrashAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var caller = await GetCallerContextAsync();
            var items = await TrashService.ListTrashAsync(caller);
            _trashedItems = items.Select(dto => new TrashItemViewModel
            {
                Id = dto.Id,
                Name = dto.Name,
                NodeType = dto.NodeType,
                Size = dto.Size,
                DeletedAt = dto.DeletedAt
            }).ToList();
        }
        catch
        {
            _trashedItems = [];
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task<CallerContext> GetCallerContextAsync()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = state.User;

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing or invalid.");
        }

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(userId, roles, CallerType.User);
    }
}
