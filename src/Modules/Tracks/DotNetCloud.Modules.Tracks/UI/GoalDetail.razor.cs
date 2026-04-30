using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Detail view for a single goal or key result with progress tracking and status management.
/// </summary>
public partial class GoalDetail : ComponentBase
{
    [Parameter] public required Guid GoalId { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    private GoalDto? _goal;
    private List<GoalDto> _keyResults = new();
    private bool _isLoading = true;
    private bool _isUpdating;
    private string? _newCurrentValue;

    protected override async Task OnParametersSetAsync()
    {
        await LoadGoalAsync();
    }

    private async Task LoadGoalAsync()
    {
        _isLoading = true;
        try
        {
            _goal = await ApiClient.GetGoalAsync(GoalId);

            if (_goal?.Type == "objective")
            {
                var allGoals = await ApiClient.ListGoalsAsync(_goal.ProductId);
                _keyResults = allGoals?.Where(g => g.ParentGoalId == _goal.Id).ToList() ?? new();
            }
        }
        catch
        {
            _goal = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task UpdateProgress()
    {
        if (_goal is null || string.IsNullOrEmpty(_newCurrentValue)) return;

        _isUpdating = true;
        try
        {
            if (double.TryParse(_newCurrentValue, out var value))
            {
                await ApiClient.UpdateGoalAsync(_goal.Id, new UpdateGoalDto
                {
                    CurrentValue = value
                });
                await LoadGoalAsync();
                _newCurrentValue = null;
            }
        }
        catch { }
        finally
        {
            _isUpdating = false;
        }
    }

    private async Task SetStatus(string status)
    {
        if (_goal is null) return;
        try
        {
            await ApiClient.UpdateGoalAsync(_goal.Id, new UpdateGoalDto { Status = status });
            await LoadGoalAsync();
        }
        catch { }
    }

    private static string GetProgressColor(double pct) => pct switch
    {
        >= 100 => "#10b981",
        >= 80 => "#3b82f6",
        >= 50 => "#f59e0b",
        _ => "#ef4444"
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
}
