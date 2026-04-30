using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Product-level roadmap view showing epics and features on a horizontal timeline
/// with milestone diamonds, dependency arrows, and today marker.
/// </summary>
public partial class ProductRoadmapView : ComponentBase
{
    [Parameter] public required Guid ProductId { get; set; }
    [Parameter] public EventCallback<Guid> OnWorkItemSelected { get; set; }

    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    private RoadmapDataDto? _data;
    private bool _isLoading = true;
    private string? _error;
    private string _zoomLevel = "month";
    private string _groupBy = "epic";
    private RoadmapItemDto? _selectedItem;
    private ElementReference _timelineRef;

    private Dictionary<string, List<RoadmapItemDto>> _groupedItems = new();
    private List<DependencyArrow> _dependencyArrows = new();
    private List<TimeHeader> _timeHeaders = new();
    private double _todayPositionPercent;
    private DateTime _timelineStart;
    private DateTime _timelineEnd;

    protected override async Task OnInitializedAsync()
    {
        await LoadRoadmapAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadRoadmapAsync();
    }

    private async Task LoadRoadmapAsync()
    {
        _isLoading = true;
        _error = null;

        try
        {
            _data = await ApiClient.GetRoadmapDataAsync(ProductId);
            ComputeTimeline();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ComputeTimeline()
    {
        if (_data is null || _data.Items.Count == 0) return;

        var now = DateTime.UtcNow;
        var allDates = _data.Items
            .SelectMany(i => new[] { i.StartDate, i.DueDate })
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();

        if (_data.Milestones.Count > 0)
            allDates.AddRange(_data.Milestones.Where(m => m.DueDate.HasValue).Select(m => m.DueDate!.Value));

        _timelineStart = allDates.Count > 0 ? allDates.Min().AddDays(-7) : now.AddMonths(-1);
        _timelineEnd = allDates.Count > 0 ? allDates.Max().AddDays(14) : now.AddMonths(3);

        // Ensure minimum range
        var minRange = _zoomLevel switch
        {
            "year" => TimeSpan.FromDays(365 * 2),
            "quarter" => TimeSpan.FromDays(180),
            _ => TimeSpan.FromDays(60)
        };

        if (_timelineEnd - _timelineStart < minRange)
            _timelineEnd = _timelineStart + minRange;

        var totalDays = (_timelineEnd - _timelineStart).TotalDays;
        _todayPositionPercent = Math.Clamp((now - _timelineStart).TotalDays / totalDays * 100.0, 0, 100);

        // Group items
        _groupedItems = _groupBy switch
        {
            "sprint" => _data.Items
                .GroupBy(i => "Active Sprint") // Simplified — would need sprint data
                .ToDictionary(g => g.Key, g => g.ToList()),
            "assignee" => _data.Items
                .GroupBy(i => i.AssigneeDisplayName ?? "Unassigned")
                .ToDictionary(g => g.Key, g => g.ToList()),
            _ => _data.Items
                .GroupBy(i => i.Type == WorkItemType.Epic ? "Epics" : "Features")
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.ToList())
        };

        // Compute dependency arrows
        _dependencyArrows = new List<DependencyArrow>();
        var itemPositions = new Dictionary<Guid, (double X, double Y)>();

        int groupIndex = 0;
        foreach (var group in _groupedItems)
        {
            foreach (var item in group.Value)
            {
                var midPos = GetDatePositionPercent(item.StartDate ?? item.DueDate);
                var endPos = GetDatePositionPercent(item.DueDate ?? item.StartDate);
                itemPositions[item.Id] = ((midPos + endPos) / 2.0, groupIndex * 60 + 30);
            }
            groupIndex++;
        }

        foreach (var item in _data.Items)
        {
            foreach (var depId in item.DependencyIds)
            {
                if (itemPositions.TryGetValue(item.Id, out var from)
                    && itemPositions.TryGetValue(depId, out var to))
                {
                    _dependencyArrows.Add(new DependencyArrow
                    {
                        Path = $"M{from.X}%,{from.Y} C{from.X}%,{(from.Y + to.Y) / 2} {to.X}%,{(from.Y + to.Y) / 2} {to.X}%,{to.Y}"
                    });
                }
            }
        }

        // Compute time headers
        _timeHeaders = new List<TimeHeader>();
        var current = _timelineStart;
        while (current < _timelineEnd)
        {
            var next = _zoomLevel switch
            {
                "year" => current.AddYears(1),
                "quarter" => current.AddMonths(3),
                _ => current.AddMonths(1)
            };
            if (next > _timelineEnd) next = _timelineEnd;

            var label = _zoomLevel switch
            {
                "year" => current.ToString("yyyy"),
                "quarter" => $"Q{(current.Month - 1) / 3 + 1} {current:yy}",
                _ => current.ToString("MMM yy")
            };

            _timeHeaders.Add(new TimeHeader
            {
                Label = label,
                PositionPercent = (current - _timelineStart).TotalDays / (_timelineEnd - _timelineStart).TotalDays * 100.0,
                WidthPercent = (next - current).TotalDays / (_timelineEnd - _timelineStart).TotalDays * 100.0,
                IsCurrent = current <= DateTime.UtcNow && next > DateTime.UtcNow
            });

            current = next;
        }
    }

    private double GetDatePositionPercent(DateTime? date)
    {
        if (!date.HasValue) return 0;
        var totalDays = (_timelineEnd - _timelineStart).TotalDays;
        if (totalDays <= 0) return 0;
        return Math.Clamp((date.Value - _timelineStart).TotalDays / totalDays * 100.0, 0, 100);
    }

    private string GetBarStyle(RoadmapItemDto item)
    {
        var startPos = GetDatePositionPercent(item.StartDate ?? item.DueDate);
        var endPos = GetDatePositionPercent(item.DueDate ?? item.StartDate);
        var widthPct = Math.Max(3.0, endPos - startPos);
        var barColor = item.SwimlaneColor ?? GetPriorityColor(item.Priority);
        return $"left: {startPos:F2}%; width: {widthPct:F2}%; background: {barColor}";
    }

    private static string GetPriorityColor(Priority priority) => priority switch
    {
        Priority.Urgent => "#ef4444",
        Priority.High => "#f59e0b",
        Priority.Medium => "#3b82f6",
        Priority.Low => "#6b7280",
        _ => "#9ca3af"
    };

    private static string GetMilestoneStyle(double position) => "left: " + position.ToString("F2") + "%";

    private static string GetMilestoneDiamondStyle(MilestoneDto milestone)
        => "background: " + (milestone.Color ?? "#6b7280");

    private static string GetMilestoneTooltip(MilestoneDto milestone)
    {
        var title = milestone.Title;
        if (milestone.DueDate.HasValue)
            title += " — " + milestone.DueDate.Value.ToString("MMM d, yyyy");
        return title;
    }

    private void SetZoom(string level)
    {
        _zoomLevel = level;
        ComputeTimeline();
    }

    private void SelectItem(RoadmapItemDto item)
    {
        _selectedItem = item;
    }

    private void CloseDetail()
    {
        _selectedItem = null;
    }

    private async Task OpenWorkItem(RoadmapItemDto item)
    {
        await OnWorkItemSelected.InvokeAsync(item.Id);
    }

    private sealed class DependencyArrow
    {
        public string Path { get; set; } = "";
    }

    private sealed class TimeHeader
    {
        public string Label { get; set; } = "";
        public double PositionPercent { get; set; }
        public double WidthPercent { get; set; }
        public bool IsCurrent { get; set; }
    }
}
