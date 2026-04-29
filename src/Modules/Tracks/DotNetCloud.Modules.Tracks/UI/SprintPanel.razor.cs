using System.ComponentModel.DataAnnotations;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Sprint management panel for an Epic — create, start, complete sprints.
/// </summary>
public partial class SprintPanel : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private BrowserTimeProvider TimeProvider { get; set; } = default!;

    [Parameter, EditorRequired] public Guid EpicId { get; set; }
    [Parameter, EditorRequired] public List<SprintDto> Sprints { get; set; } = [];
    [Parameter] public EventCallback OnSprintChanged { get; set; }
    [Parameter] public EventCallback<Guid> OnWorkItemSelected { get; set; }
    [Parameter] public EventCallback<SprintDto> OnPlanSprint { get; set; }

    private bool _showCreateDialog;
    private bool _isCreating;
    private string? _createError;
    private readonly SprintCreateModel _createModel = new();

    private bool _showEditDialog;
    private bool _isSavingEdit;
    private string? _editError;
    private Guid _editSprintId;
    private readonly SprintEditModel _editModel = new();

    private Guid? _expandedSprintId;
    private readonly List<WorkItemDto> _sprintItems = [];
    private bool _isLoadingItems;

    private bool _showItemPicker;
    private Guid _pickerSprintId;
    private string _pickerSprintTitle = "";
    private string _pickerSearch = "";
    private readonly List<WorkItemDto> _backlogItems = [];
    private readonly HashSet<Guid> _pickerSelectedItemIds = [];
    private bool _isAddingItems;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await TimeProvider.EnsureInitializedAsync();
            StateHasChanged();
        }
    }

    private void OpenCreateDialog()
    {
        _createModel.Title = "";
        _createModel.Goal = "";
        _createModel.StartDate = DateTime.Today;
        _createModel.EndDate = DateTime.Today.AddDays(14);
        _createError = null;
        _isCreating = false;
        _showCreateDialog = true;
    }

    private async Task CreateSprintAsync()
    {
        if (string.IsNullOrWhiteSpace(_createModel.Title)) return;

        _isCreating = true;
        _createError = null;

        try
        {
            await ApiClient.CreateSprintAsync(EpicId, new CreateSprintDto
            {
                Title = _createModel.Title.Trim(),
                Goal = string.IsNullOrWhiteSpace(_createModel.Goal) ? null : _createModel.Goal.Trim(),
                StartDate = _createModel.StartDate.HasValue ? _createModel.StartDate.Value.ToUniversalTime() : null,
                EndDate = _createModel.EndDate.HasValue ? _createModel.EndDate.Value.ToUniversalTime() : null
            });

            _showCreateDialog = false;
            await OnSprintChanged.InvokeAsync();
        }
        catch (Exception ex)
        {
            _createError = $"Failed to create sprint: {ex.Message}";
        }
        finally
        {
            _isCreating = false;
        }
    }

    // ── Edit Sprint ─────────────────────────────────────────

    private void OpenEditDialog(SprintDto sprint)
    {
        _editSprintId = sprint.Id;
        _editModel.Title = sprint.Title;
        _editModel.Goal = sprint.Goal ?? "";
        _editModel.StartDate = sprint.StartDate?.ToLocalTime();
        _editModel.EndDate = sprint.EndDate?.ToLocalTime();
        _editModel.TargetStoryPoints = sprint.TargetStoryPoints;
        _editError = null;
        _isSavingEdit = false;
        _showEditDialog = true;
    }

    private async Task SaveSprintEditAsync()
    {
        if (string.IsNullOrWhiteSpace(_editModel.Title)) return;

        _isSavingEdit = true;
        _editError = null;

        try
        {
            await ApiClient.UpdateSprintAsync(_editSprintId, new UpdateSprintDto
            {
                Title = _editModel.Title.Trim(),
                Goal = string.IsNullOrWhiteSpace(_editModel.Goal) ? null : _editModel.Goal.Trim(),
                StartDate = _editModel.StartDate?.ToUniversalTime(),
                EndDate = _editModel.EndDate?.ToUniversalTime(),
                TargetStoryPoints = _editModel.TargetStoryPoints
            });

            _showEditDialog = false;
            await OnSprintChanged.InvokeAsync();
        }
        catch (Exception ex)
        {
            _editError = $"Failed to update sprint: {ex.Message}";
        }
        finally
        {
            _isSavingEdit = false;
        }
    }

    private async Task StartSprintAsync(Guid sprintId)
    {
        await ApiClient.StartSprintAsync(sprintId);
        await OnSprintChanged.InvokeAsync();
    }

    private async Task CompleteSprintAsync(Guid sprintId)
    {
        await ApiClient.CompleteSprintAsync(sprintId);
        await OnSprintChanged.InvokeAsync();
    }

    private async Task DeleteSprintAsync(Guid sprintId)
    {
        await ApiClient.DeleteSprintAsync(sprintId);
        if (_expandedSprintId == sprintId)
        {
            _expandedSprintId = null;
            _sprintItems.Clear();
        }
        await OnSprintChanged.InvokeAsync();
    }

    // ── Sprint Items ──────────────────────────────────────

    private async Task ToggleSprintItems(Guid sprintId)
    {
        if (_expandedSprintId == sprintId)
        {
            _expandedSprintId = null;
            _sprintItems.Clear();
            return;
        }

        _expandedSprintId = sprintId;
        await LoadSprintItemsAsync(sprintId);
    }

    private async Task LoadSprintItemsAsync(Guid sprintId)
    {
        _isLoadingItems = true;
        try
        {
            var sprint = await ApiClient.GetSprintAsync(sprintId);
            _sprintItems.Clear();
            // Sprint items are loaded via the backlog endpoint or sprint report
            // For now, we load items via the sprint's epic child items and filter
            var allItems = await ApiClient.GetBacklogItemsAsync(EpicId);
            _sprintItems.Clear();
            _sprintItems.AddRange(allItems.Where(i => i.SprintId == sprintId));
        }
        finally
        {
            _isLoadingItems = false;
        }
    }

    private async Task RemoveItemFromSprintAsync(Guid sprintId, Guid itemId)
    {
        await ApiClient.RemoveItemFromSprintAsync(sprintId, itemId);
        _sprintItems.RemoveAll(c => c.Id == itemId);
        await OnSprintChanged.InvokeAsync();
    }

    // ── Item Picker ─────────────────────────────────────────

    private async Task OpenItemPicker(Guid sprintId)
    {
        _pickerSprintId = sprintId;
        _pickerSprintTitle = Sprints.FirstOrDefault(s => s.Id == sprintId)?.Title ?? "Sprint";
        _pickerSearch = "";
        _pickerSelectedItemIds.Clear();
        _isAddingItems = false;

        var backlog = await ApiClient.GetBacklogItemsAsync(EpicId);
        _backlogItems.Clear();
        _backlogItems.AddRange(backlog);
        _showItemPicker = true;
    }

    private void CloseItemPicker()
    {
        _showItemPicker = false;
        _pickerSelectedItemIds.Clear();
    }

    private void TogglePickerItem(Guid itemId)
    {
        if (!_pickerSelectedItemIds.Remove(itemId))
            _pickerSelectedItemIds.Add(itemId);
    }

    private IReadOnlyList<WorkItemDto> GetFilteredBacklogItems()
    {
        if (string.IsNullOrWhiteSpace(_pickerSearch))
            return _backlogItems;

        var search = _pickerSearch.Trim();
        return _backlogItems
            .Where(c => c.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || $"#{c.ItemNumber}".Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task AddSelectedItemsAsync()
    {
        if (_pickerSelectedItemIds.Count == 0) return;

        _isAddingItems = true;
        try
        {
            foreach (var itemId in _pickerSelectedItemIds)
            {
                await ApiClient.AddItemToSprintAsync(_pickerSprintId, itemId);
            }
            _showItemPicker = false;
            _pickerSelectedItemIds.Clear();

            if (_expandedSprintId == _pickerSprintId)
            {
                await LoadSprintItemsAsync(_pickerSprintId);
            }
            await OnSprintChanged.InvokeAsync();
        }
        finally
        {
            _isAddingItems = false;
        }
    }

    private static string GetStatusBadgeClass(SprintStatus status) => status switch
    {
        SprintStatus.Active => "badge-success",
        SprintStatus.Completed => "badge-muted",
        _ => "badge-info"
    };

    // Sprint completion dialog state
    private bool _showCompletionDialog;
    private SprintDto? _completionSprint;

    private void OpenCompletionDialog(SprintDto sprint)
    {
        _completionSprint = sprint;
        _showCompletionDialog = true;
    }

    private void CloseCompletionDialog()
    {
        _showCompletionDialog = false;
        _completionSprint = null;
    }

    private async Task HandleSprintCompleted()
    {
        _showCompletionDialog = false;
        _completionSprint = null;
        await OnSprintChanged.InvokeAsync();
    }

    private sealed class SprintCreateModel : IValidatableObject
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title must be 200 characters or fewer.")]
        public string Title { get; set; } = "";

        public string Goal { get; set; } = "";

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
            {
                yield return new ValidationResult(
                    "End date must be on or after the start date.",
                    [nameof(EndDate)]);
            }
        }
    }

    private sealed class SprintEditModel : IValidatableObject
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200, ErrorMessage = "Title must be 200 characters or fewer.")]
        public string Title { get; set; } = "";

        public string Goal { get; set; } = "";

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public int? TargetStoryPoints { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
            {
                yield return new ValidationResult(
                    "End date must be on or after the start date.",
                    [nameof(EndDate)]);
            }
        }
    }
}
