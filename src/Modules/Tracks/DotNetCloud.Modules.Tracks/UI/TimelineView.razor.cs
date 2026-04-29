using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Gantt-style horizontal timeline showing sprints as blocks across months.
/// Supports click-to-navigate, drag-to-adjust duration, and today marker.
/// </summary>
public partial class TimelineView : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    /// <summary>The epic whose sprint plan to display.</summary>
    [Parameter, EditorRequired] public Guid EpicId { get; set; }

    /// <summary>Called when a sprint block is clicked to navigate to the sprint-filtered kanban view.</summary>
    [Parameter] public EventCallback<SprintDto> OnSprintSelected { get; set; }

    /// <summary>Called when user wants to open the Year Plan Wizard (from empty state).</summary>
    [Parameter] public EventCallback OnOpenWizard { get; set; }

    /// <summary>Called when the plan has been adjusted (duration change with cascade).</summary>
    [Parameter] public EventCallback OnPlanAdjusted { get; set; }

    private IReadOnlyList<SprintDto>? _planOverview;
    private bool _isLoading = true;
    private string? _errorMessage;

    // Derived summary values
    private int TotalWeeks => _planOverview is { Count: > 0 }
        ? (int)Math.Ceiling((PlanEndDate - PlanStartDate).TotalDays / 7.0)
        : 0;
    private DateTime PlanStartDate => _planOverview is { Count: > 0 }
        ? _planOverview.Min(s => s.StartDate ?? DateTime.MaxValue)
        : DateTime.MinValue;
    private DateTime PlanEndDate => _planOverview is { Count: > 0 }
        ? _planOverview.Max(s => s.EndDate ?? DateTime.MinValue)
        : DateTime.MinValue;

    // Computed layout data
    private DateTime _planStart;
    private DateTime _planEnd;
    private int _totalDays;
    private int _todayColumn;
    private readonly List<MonthLabel> _monthLabels = [];

    // Drag adjust
    private SprintDto? _adjustingSprint;
    private int _adjustDurationWeeks;
    private bool _isAdjusting;

    private ElementReference _chartContainer;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadPlanAsync();
    }

    private async Task LoadPlanAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        try
        {
            _planOverview = await ApiClient.GetSprintPlanAsync(EpicId);
            if (_planOverview is not null && _planOverview.Count > 0)
            {
                ComputeLayoutData();
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load sprint plan: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshAsync()
    {
        await LoadPlanAsync();
    }

    // ── Layout Computation ──────────────────────────────────

    private void ComputeLayoutData()
    {
        if (_planOverview is null) return;

        var sprints = _planOverview;

        // Find plan boundaries — expand to include full months
        var earliestStart = sprints
            .Where(s => s.StartDate.HasValue)
            .Min(s => s.StartDate!.Value);
        var latestEnd = sprints
            .Where(s => s.EndDate.HasValue)
            .Max(s => s.EndDate!.Value);

        // Pad to first of month / end of month for clean display
        _planStart = new DateTime(earliestStart.Year, earliestStart.Month, 1);
        _planEnd = new DateTime(latestEnd.Year, latestEnd.Month, 1).AddMonths(1).AddDays(-1);

        _totalDays = (_planEnd - _planStart).Days + 1;

        // Today column
        var today = DateTime.UtcNow.Date;
        if (today >= _planStart && today <= _planEnd)
        {
            _todayColumn = (today - _planStart).Days + 1;
        }
        else
        {
            _todayColumn = 0;
        }

        // Month labels
        _monthLabels.Clear();
        var current = _planStart;
        while (current <= _planEnd)
        {
            var monthEnd = new DateTime(current.Year, current.Month, 1).AddMonths(1).AddDays(-1);
            if (monthEnd > _planEnd) monthEnd = _planEnd;

            var startDay = (current - _planStart).Days + 1;
            var endDay = (monthEnd - _planStart).Days + 1;

            _monthLabels.Add(new MonthLabel
            {
                Label = current.ToString("MMM yyyy"),
                QuarterLabel = current.Month switch
                {
                    1 or 4 or 7 or 10 => $"Q{(current.Month - 1) / 3 + 1}",
                    _ => ""
                },
                IsQuarterStart = current.Month is 1 or 4 or 7 or 10,
                StartDay = startDay,
                EndDay = endDay
            });

            current = new DateTime(current.Year, current.Month, 1).AddMonths(1);
        }
    }

    // ── Grid Style ──────────────────────────────────────────

    private string GetMonthsGridStyle()
    {
        if (_totalDays <= 0) return "";
        return $"display: grid; grid-template-columns: repeat({_totalDays}, 1fr);";
    }

    // ── Sprint Block Info ───────────────────────────────────

    private SprintBlockInfo GetSprintBlockInfo(SprintDto sprint)
    {
        if (!sprint.StartDate.HasValue || !sprint.EndDate.HasValue)
        {
            return new SprintBlockInfo { StartDay = 0, EndDay = 0, Row = 1 };
        }

        var startDay = Math.Max(1, (sprint.StartDate.Value - _planStart).Days + 1);
        var endDay = Math.Min(_totalDays, (sprint.EndDate.Value - _planStart).Days + 1);
        var row = sprint.PlannedOrder ?? 1;

        return new SprintBlockInfo { StartDay = startDay, EndDay = endDay, Row = row };
    }

    // ── Status Styling ──────────────────────────────────────

    internal static string GetSprintStatusClass(SprintStatus status) => status switch
    {
        SprintStatus.Planning => "sprint-planning",
        SprintStatus.Active => "sprint-active",
        SprintStatus.Completed => "sprint-completed",
        _ => ""
    };

    internal static double GetProgressPercent(SprintDto sprint)
    {
        if (sprint.TotalStoryPoints <= 0) return 0;
        return Math.Min(100, (double)sprint.CompletedStoryPoints / sprint.TotalStoryPoints * 100);
    }

    // ── Tooltip ─────────────────────────────────────────────

    internal static string GetSprintTooltip(SprintDto sprint)
    {
        var parts = new List<string> { sprint.Title };
        if (sprint.StartDate.HasValue && sprint.EndDate.HasValue)
        {
            parts.Add($"{sprint.StartDate.Value:MMM d} – {sprint.EndDate.Value:MMM d, yyyy}");
        }

        if (sprint.DurationWeeks.HasValue)
        {
            parts.Add($"{sprint.DurationWeeks} week(s)");
        }

        parts.Add($"{sprint.ItemCount} items, {sprint.CompletedStoryPoints}/{sprint.TotalStoryPoints} SP");
        parts.Add($"Status: {sprint.Status}");
        return string.Join(" | ", parts);
    }

    // ── Click Navigation ────────────────────────────────────

    private async Task HandleSprintClick(SprintDto sprint)
    {
        if (_adjustingSprint is not null) return; // Don't navigate while adjusting
        await OnSprintSelected.InvokeAsync(sprint);
    }

    // ── Drag Adjust ─────────────────────────────────────────

    private enum DragEdge { Start, End }

    private void StartDrag(SprintDto sprint, DragEdge edge, MouseEventArgs e)
    {
        // Open the adjust dialog instead of true drag (Blazor WASM drag limitations)
        _adjustingSprint = sprint;
        _adjustDurationWeeks = sprint.DurationWeeks ?? 2;
    }

    private void CancelAdjust()
    {
        _adjustingSprint = null;
    }

    private async Task ConfirmAdjust()
    {
        if (_adjustingSprint is null) return;
        if (_adjustDurationWeeks < 1 || _adjustDurationWeeks > 16) return;

        _isAdjusting = true;
        try
        {
            var result = await ApiClient.AdjustSprintDatesAsync(_adjustingSprint.Id, new AdjustSprintDto
            {
                DurationWeeks = _adjustDurationWeeks
            });

            if (result is not null)
            {
                _planOverview = result;
                ComputeLayoutData();
                await OnPlanAdjusted.InvokeAsync();
            }

            _adjustingSprint = null;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to adjust sprint: {ex.Message}";
        }
        finally
        {
            _isAdjusting = false;
        }
    }

    // ── Internal Models ─────────────────────────────────────

    private sealed class MonthLabel
    {
        public required string Label { get; init; }
        public required string QuarterLabel { get; init; }
        public bool IsQuarterStart { get; init; }
        public int StartDay { get; init; }
        public int EndDay { get; init; }
    }

    private struct SprintBlockInfo
    {
        public int StartDay { get; init; }
        public int EndDay { get; init; }
        public int Row { get; init; }
    }
}
