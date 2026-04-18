using System.ComponentModel.DataAnnotations;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Sprint management panel — create, start, complete sprints.
/// </summary>
public partial class SprintPanel : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;
    [Inject] private BrowserTimeProvider TimeProvider { get; set; } = default!;

    [Parameter, EditorRequired] public Guid BoardId { get; set; }
    [Parameter, EditorRequired] public List<SprintDto> Sprints { get; set; } = [];
    [Parameter] public EventCallback OnSprintChanged { get; set; }
    [Parameter] public EventCallback<Guid> OnCardSelected { get; set; }
    [Parameter] public EventCallback<SprintDto> OnPlanSprint { get; set; }

    private bool _showCreateDialog;
    private bool _isCreating;
    private string? _createError;
    private readonly SprintCreateModel _createModel = new();

    // Edit dialog state
    private bool _showEditDialog;
    private bool _isSavingEdit;
    private string? _editError;
    private Guid _editSprintId;
    private readonly SprintEditModel _editModel = new();

    // Sprint backlog state
    private Guid? _expandedSprintId;
    private readonly List<CardDto> _sprintCards = [];
    private bool _isLoadingCards;

    // Card picker state
    private bool _showCardPicker;
    private Guid _pickerSprintId;
    private string _pickerSprintTitle = "";
    private string _pickerSearch = "";
    private readonly List<CardDto> _backlogCards = [];
    private readonly HashSet<Guid> _pickerSelectedCardIds = [];
    private bool _isAddingCards;

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
            await ApiClient.CreateSprintAsync(BoardId, new CreateSprintDto
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
            await ApiClient.UpdateSprintAsync(BoardId, _editSprintId, new UpdateSprintDto
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
        await ApiClient.StartSprintAsync(BoardId, sprintId);
        await OnSprintChanged.InvokeAsync();
    }

    private async Task CompleteSprintAsync(Guid sprintId)
    {
        await ApiClient.CompleteSprintAsync(BoardId, sprintId);
        await OnSprintChanged.InvokeAsync();
    }

    private async Task DeleteSprintAsync(Guid sprintId)
    {
        await ApiClient.DeleteSprintAsync(BoardId, sprintId);
        if (_expandedSprintId == sprintId)
        {
            _expandedSprintId = null;
            _sprintCards.Clear();
        }
        await OnSprintChanged.InvokeAsync();
    }

    // ── Sprint Backlog ──────────────────────────────────────

    private async Task ToggleSprintBacklog(Guid sprintId)
    {
        if (_expandedSprintId == sprintId)
        {
            _expandedSprintId = null;
            _sprintCards.Clear();
            return;
        }

        _expandedSprintId = sprintId;
        await LoadSprintCardsAsync(sprintId);
    }

    private async Task LoadSprintCardsAsync(Guid sprintId)
    {
        _isLoadingCards = true;
        try
        {
            var cards = await ApiClient.GetSprintCardsAsync(BoardId, sprintId);
            _sprintCards.Clear();
            _sprintCards.AddRange(cards);
        }
        finally
        {
            _isLoadingCards = false;
        }
    }

    private async Task RemoveCardFromSprintAsync(Guid sprintId, Guid cardId)
    {
        await ApiClient.RemoveCardFromSprintAsync(BoardId, sprintId, cardId);
        _sprintCards.RemoveAll(c => c.Id == cardId);
        await OnSprintChanged.InvokeAsync();
    }

    // ── Card Picker ─────────────────────────────────────────

    private async Task OpenCardPicker(Guid sprintId)
    {
        _pickerSprintId = sprintId;
        _pickerSprintTitle = Sprints.FirstOrDefault(s => s.Id == sprintId)?.Title ?? "Sprint";
        _pickerSearch = "";
        _pickerSelectedCardIds.Clear();
        _isAddingCards = false;

        var backlog = await ApiClient.GetBacklogCardsAsync(BoardId);
        _backlogCards.Clear();
        _backlogCards.AddRange(backlog);
        _showCardPicker = true;
    }

    private void CloseCardPicker()
    {
        _showCardPicker = false;
        _pickerSelectedCardIds.Clear();
    }

    private void TogglePickerCard(Guid cardId)
    {
        if (!_pickerSelectedCardIds.Remove(cardId))
            _pickerSelectedCardIds.Add(cardId);
    }

    private IReadOnlyList<CardDto> GetFilteredBacklogCards()
    {
        if (string.IsNullOrWhiteSpace(_pickerSearch))
            return _backlogCards;

        var search = _pickerSearch.Trim();
        return _backlogCards
            .Where(c => c.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || $"#{c.CardNumber}".Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task AddSelectedCardsAsync()
    {
        if (_pickerSelectedCardIds.Count == 0) return;

        _isAddingCards = true;
        try
        {
            await ApiClient.BatchAddCardsToSprintAsync(BoardId, _pickerSprintId, _pickerSelectedCardIds.ToList());
            _showCardPicker = false;
            _pickerSelectedCardIds.Clear();

            // Refresh sprint cards and sprint data
            if (_expandedSprintId == _pickerSprintId)
            {
                await LoadSprintCardsAsync(_pickerSprintId);
            }
            await OnSprintChanged.InvokeAsync();
        }
        finally
        {
            _isAddingCards = false;
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
