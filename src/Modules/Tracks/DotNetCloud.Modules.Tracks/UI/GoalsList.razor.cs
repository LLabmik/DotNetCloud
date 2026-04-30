using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Goals and OKRs list with hierarchical display of objectives and key results.
/// </summary>
public partial class GoalsList : ComponentBase
{
    [Parameter] public required Guid ProductId { get; set; }
    [Parameter] public EventCallback<Guid> OnGoalSelected { get; set; }

    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    private List<GoalDto> _goals = new();
    private List<GoalDto> _objectives = new();
    private bool _isLoading = true;
    private bool _showCreateModal;
    private bool _isSaving;
    private Guid? _editingGoalId;
    private Guid? _creatingKeyResultForId;
    private string _editTitle = "";
    private string _editDescription = "";
    private string? _editTargetValue;
    private string? _editCurrentValue;
    private string _editProgressType = "manual";
    private DateTime? _editDueDate;
    private string _editError = "";

    protected override async Task OnParametersSetAsync()
    {
        await LoadGoalsAsync();
    }

    private async Task LoadGoalsAsync()
    {
        _isLoading = true;
        try
        {
            _goals = await ApiClient.ListGoalsAsync(ProductId) ?? new();
            _objectives = _goals.Where(g => g.Type == "objective").OrderBy(g => g.CreatedAt).ToList();
        }
        catch
        {
            _goals = new();
            _objectives = new();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void StartCreating()
    {
        _editingGoalId = null;
        _creatingKeyResultForId = null;
        ResetForm();
        _showCreateModal = true;
    }

    private void StartCreatingKeyResult(Guid parentObjectiveId)
    {
        _editingGoalId = null;
        _creatingKeyResultForId = parentObjectiveId;
        ResetForm();
        _showCreateModal = true;
    }

    private void ResetForm()
    {
        _editTitle = "";
        _editDescription = "";
        _editTargetValue = null;
        _editCurrentValue = null;
        _editProgressType = "manual";
        _editDueDate = null;
        _editError = "";
    }

    private async Task SaveGoal()
    {
        if (string.IsNullOrWhiteSpace(_editTitle))
        {
            _editError = "Title is required.";
            return;
        }

        _isSaving = true;
        _editError = "";

        try
        {
            double? targetValue = null;
            if (!string.IsNullOrEmpty(_editTargetValue) && double.TryParse(_editTargetValue, out var tv))
                targetValue = tv;

            double? currentValue = null;
            if (!string.IsNullOrEmpty(_editCurrentValue) && double.TryParse(_editCurrentValue, out var cv))
                currentValue = cv;

            var type = _creatingKeyResultForId.HasValue ? "key_result" : "objective";
            var parentGoalId = _creatingKeyResultForId;

            await ApiClient.CreateGoalAsync(ProductId, new CreateGoalDto
            {
                Title = _editTitle,
                Description = string.IsNullOrWhiteSpace(_editDescription) ? null : _editDescription,
                Type = type,
                ParentGoalId = parentGoalId,
                TargetValue = targetValue,
                ProgressType = _editProgressType,
                DueDate = _editDueDate
            });

            _showCreateModal = false;
            await LoadGoalsAsync();
        }
        catch (Exception ex)
        {
            _editError = ex.Message;
        }
        finally
        {
            _isSaving = false;
        }
    }

    private static string GetProgressColor(double pct) => pct switch
    {
        >= 100 => "#10b981",
        >= 80 => "#3b82f6",
        >= 50 => "#f59e0b",
        _ => "#ef4444"
    };

    private static string GetStatusColor(string status) => status switch
    {
        "completed" => "#10b981",
        "on_track" => "#3b82f6",
        "at_risk" => "#f59e0b",
        "behind" => "#ef4444",
        _ => "#9ca3af"
    };

    private static string FormatStatus(string status) => status switch
    {
        "not_started" => "Not Started",
        "on_track" => "On Track",
        "at_risk" => "At Risk",
        "behind" => "Behind",
        "completed" => "Completed",
        _ => status
    };

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
}
