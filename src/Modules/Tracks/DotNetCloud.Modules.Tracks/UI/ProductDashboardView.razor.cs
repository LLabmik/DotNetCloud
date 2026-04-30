using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// Product-level dashboard with charts and metrics: status breakdown, priority breakdown,
/// sprint progress, velocity, cycle time, workload, recent updates, upcoming due dates.
/// </summary>
public partial class ProductDashboardView : ComponentBase, IDisposable
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public ProductDto Product { get; set; } = default!;
    [Parameter] public EventCallback<Guid> OnWorkItemSelected { get; set; }

    private ProductDashboardDto? _dashboard;
    private List<SprintVelocityDto> _velocityData = [];
    private bool _isLoading = true;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            var dashboardTask = ApiClient.GetProductDashboardAsync(Product.Id);
            var velocityTask = ApiClient.GetVelocityDataAsync(Product.Id);

            await Task.WhenAll(dashboardTask, velocityTask);

            _dashboard = await dashboardTask;
            _velocityData = [.. await velocityTask];
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load dashboard: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    // ── SVG Chart Helpers ─────────────────────────────────────

    private static string BuildDonutChart(List<StatusBreakdownDto> segments, int size = 160)
    {
        if (segments.Count == 0)
            return $"<svg viewBox='0 0 {size} {size}' width='{size}' height='{size}'><circle cx='{size / 2}' cy='{size / 2}' r='{size / 2 - 10}' fill='none' stroke='#e5e7eb' stroke-width='20'/></svg>";

        var total = segments.Sum(s => s.Count);
        if (total == 0) total = 1;
        var center = size / 2.0;
        var radius = center - 14;
        var strokeWidth = 20.0;
        var circumference = 2 * Math.PI * radius;
        double currentOffset = 0;

        var parts = new List<string>();
        foreach (var segment in segments)
        {
            var proportion = segment.Count / (double)total;
            var dashLength = circumference * proportion;
            var color = segment.Color ?? GetDefaultColor(segments.IndexOf(segment));

            parts.Add($"<circle cx='{center}' cy='{center}' r='{radius}' fill='none' stroke='{color}' stroke-width='{strokeWidth}' stroke-dasharray='{dashLength:F1} {circumference - dashLength:F1}' stroke-dashoffset='{-currentOffset:F1}' transform='rotate(-90 {center} {center})'><title>{System.Net.WebUtility.HtmlEncode(segment.SwimlaneTitle)}: {segment.Count}</title></circle>");
            currentOffset += dashLength;
        }

        // Center text
        parts.Add($"<text x='{center}' y='{center}' text-anchor='middle' dominant-baseline='central' font-size='22' font-weight='700' fill='currentColor'>{total}</text>");
        parts.Add($"<text x='{center}' y='{center + 18}' text-anchor='middle' font-size='11' fill='#9ca3af'>items</text>");

        return $"<svg viewBox='0 0 {size} {size}' width='{size}' height='{size}'>{string.Join("", parts)}</svg>";
    }

    private static string BuildBarChart(List<PriorityBreakdownDto> segments, int maxHeight = 120, int barWidth = 32, int gap = 12)
    {
        if (segments.Count == 0) return "<svg viewBox='0 0 200 140' width='200' height='140'></svg>";

        var max = segments.Max(s => s.Count);
        if (max == 0) max = 1;
        var totalWidth = segments.Count * (barWidth + gap) + 20;
        var svgHeight = maxHeight + 40;

        var bars = new List<string>();
        var x = 10;
        foreach (var segment in segments)
        {
            var barHeight = (segment.Count / (double)max) * maxHeight;
            var y = maxHeight - barHeight + 10;
            var color = GetPriorityColor(segment.Priority);
            var label = segment.Priority == Priority.None ? "None" : segment.Priority.ToString();

            bars.Add($"<rect x='{x}' y='{y:F1}' width='{barWidth}' height='{barHeight:F1}' fill='{color}' rx='3'><title>{label}: {segment.Count}</title></rect>");
            bars.Add($"<text x='{x + barWidth / 2.0}' y='{maxHeight + 30}' text-anchor='middle' font-size='10' fill='#6b7280'>{label[..Math.Min(4, label.Length)]}</text>");
            bars.Add($"<text x='{x + barWidth / 2.0}' y='{y - 6:F1}' text-anchor='middle' font-size='11' font-weight='600' fill='currentColor'>{segment.Count}</text>");
            x += barWidth + gap;
        }

        return $"<svg viewBox='0 0 {totalWidth} {svgHeight}' width='{totalWidth}' height='{svgHeight}'>{string.Join("", bars)}</svg>";
    }

    private static string BuildHorizontalBarChart(List<WorkloadDto> workloads, int maxWidth = 200, int barHeight = 22)
    {
        if (workloads.Count == 0) return "<svg viewBox='0 0 300 40' width='300' height='40'></svg>";

        var max = workloads.Max(w => w.TotalStoryPoints);
        if (max == 0) max = 1;
        var svgHeight = workloads.Count * (barHeight + 6) + 10;

        var bars = new List<string>();
        var y = 8;
        foreach (var wl in workloads)
        {
            var barW = (wl.TotalStoryPoints / (double)max) * maxWidth;
            var name = wl.DisplayName ?? wl.UserId.ToString()[..8];

            bars.Add($"<text x='4' y='{y + 14}' font-size='11' fill='currentColor'>{name}</text>");
            bars.Add($"<rect x='{maxWidth + 10}' y='{y + 3}' width='{barW:F0}' height='{barHeight}' fill='#6366f1' rx='3' opacity='0.8'><title>{name}: {wl.TotalStoryPoints} SP ({wl.AssignedItems} items)</title></rect>");
            bars.Add($"<text x='{maxWidth + barW + 14:F0}' y='{y + 16}' font-size='11' font-weight='600' fill='#6366f1'>{wl.TotalStoryPoints} SP</text>");
            y += barHeight + 6;
        }

        return $"<svg viewBox='0 0 340 {svgHeight}' width='340' height='{svgHeight}'>{string.Join("", bars)}</svg>";
    }

    private static string GetDefaultColor(int index)
    {
        var colors = new[] { "#6366f1", "#8b5cf6", "#ec4899", "#f59e0b", "#10b981", "#06b6d4", "#f97316", "#84cc16" };
        return colors[index % colors.Length];
    }

    private static string GetPriorityColor(Priority priority) => priority switch
    {
        Priority.Urgent => "#ef4444",
        Priority.High => "#f97316",
        Priority.Medium => "#eab308",
        Priority.Low => "#22c55e",
        _ => "#9ca3af"
    };

    private static string GetPriorityIcon(Priority priority) => priority switch
    {
        Priority.Urgent => "🔴",
        Priority.High => "🟠",
        Priority.Medium => "🟡",
        Priority.Low => "🟢",
        _ => "⚪"
    };

    private static string GetTypeIcon(WorkItemType type) => type switch
    {
        WorkItemType.Epic => "⚡",
        WorkItemType.Feature => "🎯",
        WorkItemType.Item => "📋",
        WorkItemType.SubItem => "📌",
        _ => "📋"
    };

    private static string FormatRelativeTime(DateTime dateTime)
    {
        var diff = DateTime.UtcNow - dateTime;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return dateTime.ToString("MMM d");
    }

    private static string FormatDueDate(DateTime dueDate)
    {
        var today = DateTime.UtcNow.Date;
        var due = dueDate.Date;
        var diffDays = (due - today).Days;
        return diffDays switch
        {
            0 => "Today",
            1 => "Tomorrow",
            _ when diffDays < 0 => $"{Math.Abs(diffDays)}d overdue",
            _ => $"in {diffDays}d"
        };
    }

    public void Dispose() { }
}
