using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Full kanban board with drag-and-drop work items between swimlanes.
/// Works at any hierarchy level (Product, Epic, Feature, Item) by parameterizing the container.
/// </summary>
public partial class KanbanBoard : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private BrowserTimeProvider TimeProvider { get; set; } = default!;

    [Parameter, EditorRequired] public ProductDto Product { get; set; } = default!;
    [Parameter, EditorRequired] public SwimlaneContainerType ContainerType { get; set; }
    [Parameter, EditorRequired] public Guid ContainerId { get; set; }
    [Parameter, EditorRequired] public List<SwimlaneDto> Swimlanes { get; set; } = [];
    [Parameter, EditorRequired] public Dictionary<Guid, List<WorkItemDto>> WorkItemsBySwimlane { get; set; } = new();
    [Parameter] public List<LabelDto>? Labels { get; set; }
    [Parameter] public List<ProductMemberDto>? Members { get; set; }
    [Parameter] public WorkItemType CreateWorkItemType { get; set; } = WorkItemType.Epic;
    [Parameter] public EventCallback<Guid> OnWorkItemSelected { get; set; }
    [Parameter] public EventCallback<WorkItemDto> OnWorkItemMoved { get; set; }
    [Parameter] public EventCallback<WorkItemDto> OnWorkItemCreated { get; set; }
    [Parameter] public EventCallback<SwimlaneDto> OnSwimlaneCreated { get; set; }
    [Parameter] public EventCallback<Guid> OnSwimlaneDeleted { get; set; }
    [Parameter] public EventCallback OnRefreshRequested { get; set; }
    [Parameter] public List<SprintDto>? Sprints { get; set; }

    // Filters
    private string _filterText = "";
    private string _priorityFilter = "";
    private string _labelFilter = "";
    private string _sprintFilter = "";

    // Work item add
    private Guid? _addingItemToSwimlane;
    private string _newItemTitle = "";

    // Swimlane add
    private bool _showAddSwimlane;
    private string _newSwimlaneTitle = "";

    // Wizard
    private bool _showCreateWizard;

    // Drag state
    private WorkItemDto? _draggedItem;
    private Guid? _dropTargetItemId;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await TimeProvider.EnsureInitializedAsync();
            StateHasChanged();
        }
    }

    private IReadOnlyList<WorkItemDto> GetFilteredWorkItems(Guid swimlaneId)
    {
        if (!WorkItemsBySwimlane.TryGetValue(swimlaneId, out var items)) return [];

        IEnumerable<WorkItemDto> filtered = items.Where(c => !c.IsArchived);

        if (!string.IsNullOrWhiteSpace(_filterText))
        {
            var query = _filterText.Trim();
            filtered = filtered.Where(c => c.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(_priorityFilter) &&
            Enum.TryParse<Priority>(_priorityFilter, out var priority))
        {
            filtered = filtered.Where(c => c.Priority == priority);
        }

        if (!string.IsNullOrEmpty(_labelFilter) && Guid.TryParse(_labelFilter, out var labelId))
        {
            filtered = filtered.Where(c => c.Labels.Any(l => l.Id == labelId));
        }

        if (!string.IsNullOrEmpty(_sprintFilter))
        {
            if (_sprintFilter == "none")
            {
                filtered = filtered.Where(c => !c.SprintId.HasValue);
            }
            else if (Guid.TryParse(_sprintFilter, out var sprintId))
            {
                filtered = filtered.Where(c => c.SprintId == sprintId);
            }
        }

        return filtered.ToList();
    }

    // ── Drag & Drop ─────────────────────────────────────────

    private void HandleDragStart(WorkItemDto item) => _draggedItem = item;

    private void HandleDragEnterItem(Guid itemId)
    {
        if (_draggedItem is not null && _draggedItem.Id != itemId)
            _dropTargetItemId = itemId;
    }

    private void HandleDragOver() { /* Allow drop */ }

    private async Task HandleDropOnSwimlane(Guid targetSwimlaneId)
    {
        if (_draggedItem is null) return;

        var item = _draggedItem;
        var dropTarget = _dropTargetItemId;
        _draggedItem = null;
        _dropTargetItemId = null;

        if (item.SwimlaneId == targetSwimlaneId && dropTarget is null) return;
        if (dropTarget == item.Id) return;

        try
        {
            var targetItems = WorkItemsBySwimlane.TryGetValue(targetSwimlaneId, out var ti) ? ti : [];
            double position;

            if (dropTarget is not null)
            {
                var targetItem = targetItems.FirstOrDefault(c => c.Id == dropTarget.Value);
                if (targetItem is not null)
                {
                    var idx = targetItems.IndexOf(targetItem);
                    var sourceIdx = targetItems.FindIndex(c => c.Id == item.Id);
                    var draggingDown = sourceIdx >= 0 && sourceIdx < idx;

                    if (draggingDown)
                    {
                        if (idx >= targetItems.Count - 1)
                            position = targetItem.Position + 1000;
                        else
                            position = (targetItem.Position + targetItems[idx + 1].Position) / 2;
                    }
                    else
                    {
                        if (idx == 0)
                        {
                            position = targetItem.Position - 500;
                        }
                        else
                        {
                            var prevItem = targetItems[idx - 1];
                            if (prevItem.Id == item.Id && idx >= 2)
                                prevItem = targetItems[idx - 2];
                            else if (prevItem.Id == item.Id)
                            {
                                position = targetItem.Position - 500;
                                goto doMove;
                            }
                            position = (prevItem.Position + targetItem.Position) / 2;
                        }
                    }
                }
                else
                {
                    position = targetItems.Count > 0 ? targetItems.Max(c => c.Position) + 1000 : 1000;
                }
            }
            else
            {
                position = targetItems.Count > 0 ? targetItems.Max(c => c.Position) + 1000 : 1000;
            }

            doMove:
            var moved = await ApiClient.MoveWorkItemAsync(item.Id, new MoveWorkItemDto
            {
                TargetSwimlaneId = targetSwimlaneId,
                Position = position
            });

            if (moved is not null)
            {
                await OnWorkItemMoved.InvokeAsync(moved);
            }
        }
        catch
        {
            // Silently fail; user can refresh
        }
    }

    // ── Add Work Item ─────────────────────────────────────────

    private void BeginAddItem(Guid swimlaneId)
    {
        _addingItemToSwimlane = swimlaneId;
        _newItemTitle = "";
    }

    private void CancelAddItem()
    {
        _addingItemToSwimlane = null;
        _newItemTitle = "";
    }

    private async Task HandleAddItemKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SubmitNewItemAsync();
        else if (e.Key == "Escape") CancelAddItem();
    }

    private async Task SubmitNewItemAsync()
    {
        if (_addingItemToSwimlane is null || string.IsNullOrWhiteSpace(_newItemTitle)) return;

        try
        {
            var dto = new CreateWorkItemDto { Title = _newItemTitle.Trim() };
            WorkItemDto? item = CreateWorkItemType switch
            {
                WorkItemType.Epic => await ApiClient.CreateEpicAsync(_addingItemToSwimlane.Value, dto),
                WorkItemType.Feature => await ApiClient.CreateFeatureAsync(_addingItemToSwimlane.Value, dto),
                WorkItemType.Item => await ApiClient.CreateItemAsync(_addingItemToSwimlane.Value, dto),
                _ => null
            };

            if (item is not null)
            {
                await OnWorkItemCreated.InvokeAsync(item);
            }

            _newItemTitle = "";
        }
        catch
        {
            // Work item creation failed
        }
    }

    // ── Add Swimlane ────────────────────────────────────────

    private async Task HandleAddSwimlaneKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SubmitNewSwimlaneAsync();
        else if (e.Key == "Escape") _showAddSwimlane = false;
    }

    private async Task SubmitNewSwimlaneAsync()
    {
        if (string.IsNullOrWhiteSpace(_newSwimlaneTitle)) return;

        try
        {
            var dto = new CreateSwimlaneDto { Title = _newSwimlaneTitle.Trim() };
            SwimlaneDto? swimlane = ContainerType switch
            {
                SwimlaneContainerType.Product => await ApiClient.CreateProductSwimlaneAsync(ContainerId, dto),
                SwimlaneContainerType.WorkItem => await ApiClient.CreateWorkItemSwimlaneAsync(ContainerId, dto),
                _ => null
            };

            if (swimlane is not null)
            {
                await OnSwimlaneCreated.InvokeAsync(swimlane);
            }

            _newSwimlaneTitle = "";
            _showAddSwimlane = false;
        }
        catch
        {
            // Swimlane creation failed
        }
    }

    private async Task DeleteSwimlaneAsync(Guid swimlaneId)
    {
        try
        {
            await ApiClient.DeleteSwimlaneAsync(swimlaneId);
            await OnSwimlaneDeleted.InvokeAsync(swimlaneId);
        }
        catch
        {
            // Deletion failed
        }
    }

    // ── Helpers ─────────────────────────────────────────────

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

    private static bool IsOverdue(DateTime dueDate) => dueDate < DateTime.UtcNow;

    private static bool IsDueSoon(DateTime dueDate) =>
        dueDate >= DateTime.UtcNow && dueDate <= DateTime.UtcNow.AddDays(2);

    /// <summary>
    /// Determines whether white or dark text should be used on a given background color
    /// using the W3C relative luminance formula.
    /// </summary>
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

    /// <summary>
    /// Builds the inline style for the card header using the product's color.
    /// </summary>
    private string GetCardHeaderStyle()
    {
        if (string.IsNullOrEmpty(Product.Color))
            return "background: var(--color-primary); color: #fff;";

        var textColor = GetContrastTextColor(Product.Color);
        return $"background: {Product.Color}; color: {textColor};";
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name[..1].ToUpperInvariant();
    }

    // ── Hierarchy Visuals ──────────────────────────────────

    private string GetLevelIcon() => CreateWorkItemType switch
    {
        WorkItemType.Epic => "🏗️",
        WorkItemType.Feature => "🎯",
        WorkItemType.Item => "📋",
        WorkItemType.SubItem => "📌",
        _ => "📋"
    };

    private string GetLevelLabel() => CreateWorkItemType switch
    {
        WorkItemType.Epic => "Epics",
        WorkItemType.Feature => "Features",
        WorkItemType.Item => "Items",
        WorkItemType.SubItem => "Sub-Items",
        _ => "Work Items"
    };

    private int GetTotalCardCount()
    {
        return WorkItemsBySwimlane.Values.Sum(items => items.Count(c => !c.IsArchived));
    }

    private string GetDepthClass() => CreateWorkItemType switch
    {
        WorkItemType.Epic => "tracks-kanban--depth-1",
        WorkItemType.Feature => "tracks-kanban--depth-2",
        WorkItemType.Item => "tracks-kanban--depth-3",
        WorkItemType.SubItem => "tracks-kanban--depth-4",
        _ => ""
    };

    // ── Wizard ─────────────────────────────────────────────

    private void OpenCreateWizard()
    {
        _showCreateWizard = true;
    }

    private void CloseCreateWizard()
    {
        _showCreateWizard = false;
    }

    private async Task HandleWizardCreated(WorkItemDto item)
    {
        _showCreateWizard = false;
        await OnWorkItemCreated.InvokeAsync(item);
    }
}
