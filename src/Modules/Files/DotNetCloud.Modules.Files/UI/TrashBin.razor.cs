using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the trash bin component.
/// </summary>
public partial class TrashBin : ComponentBase
{
    /// <summary>Number of days items are retained before permanent deletion (displayed in the empty state).</summary>
    [Parameter] public int RetentionDays { get; set; } = 30;

    private List<TrashItemViewModel> _trashedItems = [];
    private readonly HashSet<Guid> _selectedItems = [];
    private string _sortColumn = "Date";
    private bool _sortAscending;

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
    protected void RestoreSelected()
    {
        _trashedItems.RemoveAll(i => _selectedItems.Contains(i.Id));
        _selectedItems.Clear();
    }

    /// <summary>Permanently deletes all selected items.</summary>
    protected void DeleteSelected()
    {
        _trashedItems.RemoveAll(i => _selectedItems.Contains(i.Id));
        _selectedItems.Clear();
    }

    /// <summary>Restores a single item.</summary>
    protected void RestoreItem(Guid itemId)
    {
        _trashedItems.RemoveAll(i => i.Id == itemId);
        _selectedItems.Remove(itemId);
    }

    /// <summary>Permanently deletes a single item.</summary>
    protected void PurgeItem(Guid itemId)
    {
        _trashedItems.RemoveAll(i => i.Id == itemId);
        _selectedItems.Remove(itemId);
    }

    /// <summary>Permanently deletes all items in the trash.</summary>
    protected void EmptyTrash()
    {
        _trashedItems.Clear();
        _selectedItems.Clear();
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
}
