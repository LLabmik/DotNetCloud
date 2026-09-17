using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Diagnostics;
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
    private bool _isEmptying;
    private bool _isRestoring;
    private string _restoreStatus = string.Empty;
    private bool _isDeletingSelected;
    private string _deleteStatus = string.Empty;

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
            ("Name", true) => [.. _trashedItems.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)],
            ("Name", false) => [.. _trashedItems.OrderByDescending(i => i.Name, StringComparer.OrdinalIgnoreCase)],
            ("Size", true) => [.. _trashedItems.OrderBy(i => i.Size)],
            ("Size", false) => [.. _trashedItems.OrderByDescending(i => i.Size)],
            ("Date", true) => [.. _trashedItems.OrderBy(i => i.DeletedAt)],
            _ => [.. _trashedItems.OrderByDescending(i => i.DeletedAt)]
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

    /// <summary>Whether the empty-trash operation is currently in progress.</summary>
    protected bool IsEmptying => _isEmptying;

    /// <summary>Whether a restore operation is currently in progress.</summary>
    protected bool IsRestoring => _isRestoring;

    /// <summary>Progress text shown next to the restore spinner (e.g. "Restoring 2 of 5: Photos…").</summary>
    protected string RestoreStatus => _restoreStatus;

    /// <summary>Whether a permanent delete of the selected items is currently in progress.</summary>
    protected bool IsDeletingSelected => _isDeletingSelected;

    /// <summary>Progress text shown next to the delete spinner (e.g. "Deleting 2 of 5: Photos…").</summary>
    protected string DeleteStatus => _deleteStatus;

    /// <summary>Whether any long-running trash operation is in progress.</summary>
    protected bool IsBusy => _isEmptying || _isRestoring || _isDeletingSelected;

    /// <summary>Returns whether the given item is currently selected.</summary>
    protected bool IsSelected(Guid id) => _selectedItems.Contains(id);

    /// <summary>Toggles selection of a single item.</summary>
    protected void ToggleSelect(Guid id)
    {
        if (!_selectedItems.Add(id))
            _selectedItems.Remove(id);
    }

    /// <summary>Selects all items or deselects all, depending on current state.</summary>
    protected void ToggleSelectAll()
    {
        if (IsAllSelected)
            _selectedItems.Clear();
        else
            foreach (var item in _trashedItems)
                _selectedItems.Add(item.Id);
    }

    /// <summary>Restores selected items back to their original location, showing progress while it runs.</summary>
    protected async Task RestoreSelected()
    {
        if (_isRestoring)
            return;

        var items = _trashedItems.Where(i => _selectedItems.Contains(i.Id)).ToList();
        if (items.Count == 0)
            return;

        _isRestoring = true;
        try
        {
            var caller = await GetCallerContextAsync();

            for (var index = 0; index < items.Count; index++)
            {
                _restoreStatus = items.Count == 1
                    ? $"Restoring {items[index].Name}…"
                    : $"Restoring {index + 1} of {items.Count}: {items[index].Name}…";
                StateHasChanged();

                await TrashService.RestoreAsync(items[index].Id, caller);
                _selectedItems.Remove(items[index].Id);
            }
        }
        finally
        {
            _isRestoring = false;
            _restoreStatus = string.Empty;
        }

        await LoadTrashAsync();
        await OnTrashChanged.InvokeAsync();
    }

    /// <summary>Permanently deletes all selected items, showing progress while it runs.</summary>
    protected async Task DeleteSelected()
    {
        if (IsBusy)
            return;

        var items = _trashedItems.Where(i => _selectedItems.Contains(i.Id)).ToList();
        if (items.Count == 0)
            return;

        _isDeletingSelected = true;
        StateHasChanged();
        var deleteProgress = Stopwatch.StartNew();

        // Hand the renderer a real async gap — see FileBrowser.ConfirmDeleteAsync.
        await Task.Delay(1);

        try
        {
            var caller = await GetCallerContextAsync();

            for (var index = 0; index < items.Count; index++)
            {
                _deleteStatus = FilesDeleteProgress.BuildStatus(index, items.Count, items[index].Name);
                StateHasChanged();

                await TrashService.PermanentDeleteAsync(items[index].Id, caller);
                _selectedItems.Remove(items[index].Id);
            }
        }
        finally
        {
            // See FilesDeleteProgress.GetHoldTimeMs — fast deletes are otherwise swallowed by
            // Blazor's render batching and the user never sees the progress banner.
            var holdMs = FilesDeleteProgress.GetHoldTimeMs((int)deleteProgress.ElapsedMilliseconds);
            if (holdMs > 0)
                await Task.Delay(holdMs);

            _isDeletingSelected = false;
            _deleteStatus = string.Empty;
        }

        await LoadTrashAsync();
        await OnTrashChanged.InvokeAsync();
    }

    /// <summary>Restores a single item, showing a spinner while it runs.</summary>
    protected async Task RestoreItem(Guid itemId)
    {
        if (_isRestoring)
            return;

        _isRestoring = true;
        _restoreStatus = "Restoring…";
        StateHasChanged();

        try
        {
            var caller = await GetCallerContextAsync();
            await TrashService.RestoreAsync(itemId, caller);
            _selectedItems.Remove(itemId);
        }
        finally
        {
            _isRestoring = false;
            _restoreStatus = string.Empty;
        }

        await LoadTrashAsync();
        await OnTrashChanged.InvokeAsync();
    }

    /// <summary>Permanently deletes a single item.</summary>
    protected async Task PurgeItem(Guid itemId)
    {
        if (IsBusy)
            return;

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

    /// <summary>Permanently deletes all items in the trash, showing a spinner while in progress.</summary>
    protected async Task EmptyTrash()
    {
        if (IsBusy)
            return;

        _isEmptying = true;
        StateHasChanged();
        try
        {
            var caller = await GetCallerContextAsync();
            await TrashService.EmptyTrashAsync(caller);
            _selectedItems.Clear();
            _showEmptyConfirm = false;
            await LoadTrashAsync();
            await OnTrashChanged.InvokeAsync();
        }
        finally
        {
            _isEmptying = false;
            StateHasChanged();
        }
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
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
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
                DeletedAt = dto.DeletedAt,
                OriginalPath = string.IsNullOrWhiteSpace(dto.OriginalPath) ? "/" : dto.OriginalPath
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
