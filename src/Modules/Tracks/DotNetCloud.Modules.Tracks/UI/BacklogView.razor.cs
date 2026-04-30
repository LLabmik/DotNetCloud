using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Backlog view showing items not assigned to any sprint.
/// Supports filtering, multi-select, and bulk sprint assignment.
/// </summary>
public partial class BacklogView : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public Guid EpicId { get; set; }
    [Parameter, EditorRequired] public List<SprintDto> Sprints { get; set; } = [];
    [Parameter] public List<SwimlaneDto> Swimlanes { get; set; } = [];
    [Parameter] public EventCallback<Guid> OnWorkItemSelected { get; set; }
    [Parameter] public EventCallback OnBacklogChanged { get; set; }

    private readonly List<WorkItemDto> _backlogItems = [];
    private readonly HashSet<Guid> _selectedItemIds = [];
    private bool _isLoading = true;
    private bool _isBulkAssigning;
    private bool _selectAll;
    private string _filterText = "";
    private string _priorityFilter = "";
    private string _bulkTargetSprintId = "";

    // ── Add Card State ──────────────────────────────────────
    private bool _isAddingCard;
    private bool _isSubmittingCard;
    private string _newCardTitle = "";
    private string _newCardSwimlaneId = "";
    private string _newCardPriority = "";

    // Export
    private bool _isExporting;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadBacklogAsync();
    }

    private async Task LoadBacklogAsync()
    {
        _isLoading = true;
        try
        {
            var items = await ApiClient.GetBacklogItemsAsync(EpicId);
            _backlogItems.Clear();
            _backlogItems.AddRange(items.Where(i => i.SprintId == null));
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshBacklogAsync()
    {
        _selectedItemIds.Clear();
        _selectAll = false;
        await LoadBacklogAsync();
    }

    private IReadOnlyList<WorkItemDto> GetFilteredItems()
    {
        IEnumerable<WorkItemDto> filtered = _backlogItems.Where(i => !i.IsArchived);

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            var query = _filterText.Trim();
            filtered = filtered.Where(i =>
                i.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                $"#{i.ItemNumber}".Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(_priorityFilter) &&
            Enum.TryParse<Priority>(_priorityFilter, out var priority))
        {
            filtered = filtered.Where(i => i.Priority == priority);
        }

        return filtered.OrderByDescending(i => i.Priority).ThenBy(i => i.CreatedAt).ToList();
    }

    // ── Selection ───────────────────────────────────────────

    private void ToggleItemSelection(Guid itemId)
    {
        if (!_selectedItemIds.Remove(itemId))
            _selectedItemIds.Add(itemId);

        _selectAll = _selectedItemIds.Count == GetFilteredItems().Count && _selectedItemIds.Count > 0;
    }

    private void ToggleSelectAll()
    {
        _selectAll = !_selectAll;
        _selectedItemIds.Clear();

        if (_selectAll)
        {
            foreach (var item in GetFilteredItems())
                _selectedItemIds.Add(item.Id);
        }
    }

    // ── Sprint Assignment ───────────────────────────────────

    private async Task AssignItemToSprintAsync(Guid itemId, ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (string.IsNullOrEmpty(value) || !Guid.TryParse(value, out var sprintId)) return;

        await ApiClient.AddItemToSprintAsync(sprintId, itemId);
        _backlogItems.RemoveAll(i => i.Id == itemId);
        _selectedItemIds.Remove(itemId);
        await OnBacklogChanged.InvokeAsync();
    }

    private async Task BulkAssignToSprintAsync()
    {
        if (string.IsNullOrEmpty(_bulkTargetSprintId) ||
            !Guid.TryParse(_bulkTargetSprintId, out var sprintId) ||
            _selectedItemIds.Count == 0)
            return;

        _isBulkAssigning = true;
        try
        {
            foreach (var itemId in _selectedItemIds)
            {
                await ApiClient.AddItemToSprintAsync(sprintId, itemId);
            }
            _backlogItems.RemoveAll(i => _selectedItemIds.Contains(i.Id));
            _selectedItemIds.Clear();
            _selectAll = false;
            _bulkTargetSprintId = "";
            await OnBacklogChanged.InvokeAsync();
        }
        finally
        {
            _isBulkAssigning = false;
        }
    }

    // ── Helpers ─────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Default swimlane selection to first available when not set
        if (string.IsNullOrEmpty(_newCardSwimlaneId) && Swimlanes.Count > 0)
            _newCardSwimlaneId = Swimlanes[0].Id.ToString();
    }

    // ── Add Card ────────────────────────────────────────────

    private void ToggleAddCard()
    {
        _isAddingCard = !_isAddingCard;
        if (_isAddingCard)
        {
            _newCardTitle = "";
            _newCardPriority = "";
            if (Swimlanes.Count > 0)
                _newCardSwimlaneId = Swimlanes[0].Id.ToString();
        }
    }

    private void CancelAddCard()
    {
        _isAddingCard = false;
        _newCardTitle = "";
        _newCardPriority = "";
    }

    private async Task HandleNewCardKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SubmitNewCardAsync();
        else if (e.Key == "Escape") CancelAddCard();
    }

    private async Task SubmitNewCardAsync()
    {
        if (string.IsNullOrWhiteSpace(_newCardTitle) || Swimlanes.Count == 0) return;

        if (!Guid.TryParse(_newCardSwimlaneId, out var swimlaneId))
            swimlaneId = Swimlanes[0].Id;

        var priority = Priority.None;
        if (!string.IsNullOrEmpty(_newCardPriority))
            Enum.TryParse(_newCardPriority, out priority);

        _isSubmittingCard = true;
        try
        {
            var item = await ApiClient.CreateItemAsync(swimlaneId, new CreateWorkItemDto
            {
                Title = _newCardTitle.Trim(),
                Priority = priority
            });

            if (item is not null)
            {
                _backlogItems.Insert(0, item);
                _newCardTitle = "";
                _newCardPriority = "";
                await OnBacklogChanged.InvokeAsync();
            }
        }
        finally
        {
            _isSubmittingCard = false;
        }
    }

    // ── Display Helpers ─────────────────────────────────────

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

    private static string GetContrastTextColor(string? hexColor)
    {
        if (string.IsNullOrEmpty(hexColor)) return "#fff";

        var hex = hexColor.TrimStart('#');
        if (hex.Length == 3)
            hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";

        if (hex.Length != 6 || !int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out _))
            return "#fff";

        var r = int.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber) / 255.0;
        var g = int.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber) / 255.0;
        var b = int.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber) / 255.0;

        r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);

        var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return luminance > 0.179 ? "#1a1a2e" : "#ffffff";
    }

    // ── CSV Export ──────────────────────────────────────────

    /// <summary>
    /// Downloads the back-log as a spreadsheet (CSV file).
    /// The file can be opened in Excel, Google Sheets, or Numbers.
    /// </summary>
    private async Task ExportToCsvAsync()
    {
        _isExporting = true;
        try
        {
            Priority? priority = null;
            if (!string.IsNullOrEmpty(_priorityFilter) && Enum.TryParse<Priority>(_priorityFilter, out var p))
                priority = p;

            var csvBytes = await ApiClient.ExportWorkItemsCsvAsync(
                EpicId, swimlaneId: null, labelId: null, priority: priority);

            await DownloadFileAsync(csvBytes, $"backlog-export-{DateTime.UtcNow:yyyy-MM-dd}.csv");
        }
        catch
        {
            // Export failed silently
        }
        finally
        {
            _isExporting = false;
        }
    }

    /// <summary>
    /// Triggers a browser file download using JavaScript interop.
    /// </summary>
    private async Task DownloadFileAsync(byte[] bytes, string fileName)
    {
        var base64 = Convert.ToBase64String(bytes);
        await JS.InvokeVoidAsync("eval",
            $"var a=document.createElement('a');a.href='data:text/csv;base64,{base64}';a.download='{fileName}';document.body.appendChild(a);a.click();document.body.removeChild(a);");
    }
}
