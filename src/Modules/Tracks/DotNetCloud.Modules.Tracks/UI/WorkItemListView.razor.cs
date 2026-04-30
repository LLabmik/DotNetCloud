using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Sortable, filterable data table view of all work items in a product.
/// Supports multi-select, bulk actions, inline editing, column customization,
/// grouping, and CSV export.
/// </summary>
public partial class WorkItemListView : ComponentBase, IDisposable
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter, EditorRequired] public ProductDto Product { get; set; } = default!;
    [Parameter] public List<SwimlaneDto> Swimlanes { get; set; } = [];
    [Parameter] public List<LabelDto> Labels { get; set; } = [];
    [Parameter] public List<ProductMemberDto> Members { get; set; } = [];
    [Parameter] public List<SprintDto>? Sprints { get; set; }
    [Parameter] public EventCallback<Guid> OnWorkItemSelected { get; set; }
    [Parameter] public EventCallback OnItemsChanged { get; set; }

    private List<WorkItemDto> _allItems = [];
    private List<WorkItemDto> _filteredItems = [];
    private bool _isLoading = true;
    private string? _errorMessage;

    // Sorting
    private string _sortColumn = "ItemNumber";
    private bool _sortAscending = true;

    // Filtering
    private string _filterText = "";
    private Priority? _priorityFilter;
    private Guid? _swimlaneFilter;
    private Guid? _labelFilter;
#pragma warning disable CS0649 // Assigned via Blazor @bind in .razor file
    private Guid? _assigneeFilter;
#pragma warning restore CS0649

    // Selection
    private readonly HashSet<Guid> _selectedIds = [];
    private bool _selectAll;

    // Column visibility
    private readonly HashSet<string> _visibleColumns = new([
        "ItemNumber", "Title", "Type", "Priority", "Swimlane",
        "Assignee", "StoryPoints", "DueDate", "Labels", "Sprint"
    ]);
    private bool _showColumnChooser;

    // Grouping
    private string? _groupBy; // null, "Assignee", "Priority", "Swimlane", "Sprint", "Type"

    // Inline editing
    private Guid? _editingCellItemId;
    private string? _editingCellColumn;

    // Bulk actions
    private bool _isBulkActing;
    private Guid? _bulkTargetSwimlaneId;
    private Guid? _bulkLabelId;
    private Guid? _bulkAssigneeId;
    private Guid? _bulkSprintId;
    private bool _showBulkMoveDropdown;
    private bool _showBulkLabelDropdown;
    private bool _showBulkAssignDropdown;
    private bool _showBulkPriorityDropdown;
    private bool _showBulkSprintDropdown;

    // Export
    private bool _isExporting;

    // Column widths
    private readonly Dictionary<string, double> _columnWidths = new()
    {
        ["checkbox"] = 40,
        ["ItemNumber"] = 90,
        ["Title"] = 300,
        ["Type"] = 80,
        ["Priority"] = 80,
        ["Swimlane"] = 130,
        ["Assignee"] = 130,
        ["StoryPoints"] = 90,
        ["DueDate"] = 110,
        ["Labels"] = 150,
        ["Sprint"] = 130
    };

    public static readonly string[] AllColumns = [
        "ItemNumber", "Title", "Type", "Priority", "Swimlane",
        "Assignee", "StoryPoints", "DueDate", "Labels", "Sprint"
    ];

    private static readonly string[] GroupByOptions = ["None", "Assignee", "Priority", "Swimlane", "Sprint", "Type"];

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            _allItems = [.. await ApiClient.ListProductWorkItemsAsync(Product.Id)];
            ApplyFiltersAndSort();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load items: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyFiltersAndSort()
    {
        var query = _allItems.AsEnumerable();

        // Text filter
        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            var filter = _filterText.Trim();
            query = query.Where(i =>
                i.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                i.ItemNumber.ToString().Contains(filter));
        }

        // Dropdown filters
        if (_priorityFilter.HasValue)
            query = query.Where(i => i.Priority == _priorityFilter.Value);
        if (_swimlaneFilter.HasValue)
            query = query.Where(i => i.SwimlaneId == _swimlaneFilter.Value);
        if (_labelFilter.HasValue)
            query = query.Where(i => i.Labels.Any(l => l.Id == _labelFilter.Value));
        if (_assigneeFilter.HasValue)
            query = query.Where(i => i.Assignments.Any(a => a.UserId == _assigneeFilter.Value));

        // Sort
        query = _sortColumn switch
        {
            "ItemNumber" => _sortAscending ? query.OrderBy(i => i.ItemNumber) : query.OrderByDescending(i => i.ItemNumber),
            "Title" => _sortAscending ? query.OrderBy(i => i.Title) : query.OrderByDescending(i => i.Title),
            "Type" => _sortAscending ? query.OrderBy(i => i.Type) : query.OrderByDescending(i => i.Type),
            "Priority" => _sortAscending ? query.OrderBy(i => (int)i.Priority) : query.OrderByDescending(i => (int)i.Priority),
            "Swimlane" => _sortAscending ? query.OrderBy(i => i.SwimlaneTitle) : query.OrderByDescending(i => i.SwimlaneTitle),
            "Assignee" => _sortAscending ? query.OrderBy(i => i.Assignments.FirstOrDefault()?.DisplayName) : query.OrderByDescending(i => i.Assignments.FirstOrDefault()?.DisplayName),
            "StoryPoints" => _sortAscending ? query.OrderBy(i => i.StoryPoints ?? 0) : query.OrderByDescending(i => i.StoryPoints ?? 0),
            "DueDate" => _sortAscending ? query.OrderBy(i => i.DueDate ?? DateTime.MaxValue) : query.OrderByDescending(i => i.DueDate ?? DateTime.MinValue),
            "Labels" => _sortAscending ? query.OrderBy(i => i.Labels.FirstOrDefault()?.Title) : query.OrderByDescending(i => i.Labels.FirstOrDefault()?.Title),
            "Sprint" => _sortAscending ? query.OrderBy(i => i.SprintTitle) : query.OrderByDescending(i => i.SprintTitle),
            _ => query
        };

        _filteredItems = query.ToList();
        _selectAll = false;
        _selectedIds.Clear();
    }

    private void ToggleSort(string column)
    {
        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }
        ApplyFiltersAndSort();
    }

    private string GetSortIndicator(string column)
    {
        if (_sortColumn != column) return "";
        return _sortAscending ? " ▲" : " ▼";
    }

    // ── Selection ────────────────────────────────────────────

    private void ToggleSelectAll()
    {
        if (_selectAll)
        {
            _selectedIds.Clear();
            _selectAll = false;
        }
        else
        {
            foreach (var item in _filteredItems)
                _selectedIds.Add(item.Id);
            _selectAll = true;
        }
    }

    private void ToggleItemSelection(Guid itemId)
    {
        if (_selectedIds.Contains(itemId))
            _selectedIds.Remove(itemId);
        else
            _selectedIds.Add(itemId);
        _selectAll = _selectedIds.Count == _filteredItems.Count && _filteredItems.Count > 0;
    }

    // ── Column Chooser ───────────────────────────────────────

    private void ToggleColumn(string column)
    {
        if (_visibleColumns.Contains(column))
        {
            if (_visibleColumns.Count > 1)
                _visibleColumns.Remove(column);
        }
        else
            _visibleColumns.Add(column);
    }

    // ── Bulk Actions ─────────────────────────────────────────

    private async Task BulkArchiveAsync()
    {
        if (_selectedIds.Count == 0) return;
        _isBulkActing = true;
        try
        {
            await ApiClient.BulkWorkItemActionAsync(Product.Id, new BulkWorkItemActionDto
            {
                WorkItemIds = _selectedIds.ToList(),
                Action = "archive"
            });
            await ReloadAsync();
        }
        finally { _isBulkActing = false; }
    }

    private async Task BulkDeleteAsync()
    {
        if (_selectedIds.Count == 0) return;
        _isBulkActing = true;
        try
        {
            await ApiClient.BulkWorkItemActionAsync(Product.Id, new BulkWorkItemActionDto
            {
                WorkItemIds = _selectedIds.ToList(),
                Action = "delete"
            });
            await ReloadAsync();
        }
        finally { _isBulkActing = false; }
    }

    private async Task BulkMoveAsync()
    {
        if (_selectedIds.Count == 0 || !_bulkTargetSwimlaneId.HasValue) return;
        _isBulkActing = true;
        try
        {
            await ApiClient.BulkWorkItemActionAsync(Product.Id, new BulkWorkItemActionDto
            {
                WorkItemIds = _selectedIds.ToList(),
                Action = "move",
                TargetSwimlaneId = _bulkTargetSwimlaneId
            });
            _showBulkMoveDropdown = false;
            await ReloadAsync();
        }
        finally { _isBulkActing = false; }
    }

    private async Task BulkAddLabelAsync()
    {
        if (_selectedIds.Count == 0 || !_bulkLabelId.HasValue) return;
        _isBulkActing = true;
        try
        {
            await ApiClient.BulkWorkItemActionAsync(Product.Id, new BulkWorkItemActionDto
            {
                WorkItemIds = _selectedIds.ToList(),
                Action = "add-label",
                LabelId = _bulkLabelId
            });
            _showBulkLabelDropdown = false;
            await ReloadAsync();
        }
        finally { _isBulkActing = false; }
    }

    private async Task BulkAssignAsync()
    {
        if (_selectedIds.Count == 0 || !_bulkAssigneeId.HasValue) return;
        _isBulkActing = true;
        try
        {
            await ApiClient.BulkWorkItemActionAsync(Product.Id, new BulkWorkItemActionDto
            {
                WorkItemIds = _selectedIds.ToList(),
                Action = "assign",
                AssigneeUserId = _bulkAssigneeId
            });
            _showBulkAssignDropdown = false;
            await ReloadAsync();
        }
        finally { _isBulkActing = false; }
    }

    private async Task BulkSetPriorityAsync(Priority priority)
    {
        if (_selectedIds.Count == 0) return;
        _isBulkActing = true;
        try
        {
            await ApiClient.BulkWorkItemActionAsync(Product.Id, new BulkWorkItemActionDto
            {
                WorkItemIds = _selectedIds.ToList(),
                Action = "set-priority",
                Priority = priority
            });
            _showBulkPriorityDropdown = false;
            await ReloadAsync();
        }
        finally { _isBulkActing = false; }
    }

    private async Task BulkAssignSprintAsync()
    {
        if (_selectedIds.Count == 0 || !_bulkSprintId.HasValue) return;
        _isBulkActing = true;
        try
        {
            await ApiClient.BulkWorkItemActionAsync(Product.Id, new BulkWorkItemActionDto
            {
                WorkItemIds = _selectedIds.ToList(),
                Action = "assign-sprint",
                SprintId = _bulkSprintId
            });
            _showBulkSprintDropdown = false;
            await ReloadAsync();
        }
        finally { _isBulkActing = false; }
    }

    private async Task ReloadAsync()
    {
        await LoadDataAsync();
        await OnItemsChanged.InvokeAsync();
    }

    // ── Export ───────────────────────────────────────────────

    private async Task ExportToCsvAsync()
    {
        _isExporting = true;
        try
        {
            var bytes = await ApiClient.ExportWorkItemsCsvAsync(Product.Id,
                swimlaneId: _swimlaneFilter, labelId: _labelFilter, priority: _priorityFilter);
            var base64 = Convert.ToBase64String(bytes);
            await JS.InvokeVoidAsync("tracksList.downloadCsv", base64,
                $"{SanitizeFileName(Product.Name)}-export-{DateTime.UtcNow:yyyy-MM-dd}.csv");
        }
        finally { _isExporting = false; }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "export" : sanitized.Trim();
    }

    // ── Grouping ─────────────────────────────────────────────

    private List<IGrouping<string?, WorkItemDto>> GetGroupedItems()
    {
        if (string.IsNullOrEmpty(_groupBy) || _groupBy == "None")
            return [];

        return _groupBy switch
        {
            "Assignee" => [.. _filteredItems
                .GroupBy(i => i.Assignments.FirstOrDefault()?.UserId.ToString() ?? "Unassigned")
                .OrderBy(g => g.Key)],
            "Priority" => [.. _filteredItems
                .GroupBy(i => i.Priority.ToString())
                .OrderByDescending(g => (int)g.First().Priority)],
            "Swimlane" => [.. _filteredItems
                .GroupBy(i => i.SwimlaneTitle ?? "No Status")
                .OrderBy(g => g.Key)],
            "Sprint" => [.. _filteredItems
                .GroupBy(i => i.SprintTitle ?? "No Sprint")
                .OrderBy(g => g.Key)],
            "Type" => [.. _filteredItems
                .GroupBy(i => i.Type.ToString())
                .OrderBy(g => g.Key)],
            _ => []
        };
    }

    // ── Helpers ──────────────────────────────────────────────

    private static string GetPriorityClass(Priority priority) => priority switch
    {
        Priority.Urgent => "priority-urgent",
        Priority.High => "priority-high",
        Priority.Medium => "priority-medium",
        Priority.Low => "priority-low",
        _ => ""
    };

    private static string GetPriorityIcon(Priority priority) => priority switch
    {
        Priority.Urgent => "🔴",
        Priority.High => "🟠",
        Priority.Medium => "🟡",
        Priority.Low => "🟢",
        _ => ""
    };

    private static string GetTypeIcon(WorkItemType type) => type switch
    {
        WorkItemType.Epic => "⚡",
        WorkItemType.Feature => "🎯",
        WorkItemType.Item => "📋",
        WorkItemType.SubItem => "📌",
        _ => "📋"
    };

    private string GetAssigneeDisplay(WorkItemDto item)
    {
        var assignments = item.Assignments;
        if (assignments.Count == 0) return "—";
        return string.Join(", ", assignments.Select(a =>
        {
            var member = Members.FirstOrDefault(m => m.UserId == a.UserId);
            return member?.DisplayName ?? a.UserId.ToString()[..8];
        }));
    }

    private string GetContrastTextColor(string? hexColor)
    {
        if (string.IsNullOrEmpty(hexColor) || hexColor.Length < 7) return "#333";
        try
        {
            var r = Convert.ToInt32(hexColor[1..3], 16);
            var g = Convert.ToInt32(hexColor[3..5], 16);
            var b = Convert.ToInt32(hexColor[5..7], 16);
            var luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
            return luminance > 0.6 ? "#1a1a1a" : "#ffffff";
        }
        catch { return "#333"; }
    }

    public void Dispose()
    {
        // No subscriptions to clean up
    }
}
