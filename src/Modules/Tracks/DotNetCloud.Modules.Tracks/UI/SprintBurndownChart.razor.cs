using System.Globalization;
using System.Text;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// SVG-based sprint burndown chart showing ideal vs actual remaining story points.
/// </summary>
public partial class SprintBurndownChart : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public Guid SprintId { get; set; }

    private SprintReportDto? _report;
    private SprintBurndownDto? _burndownData;
    private bool _isLoading = true;

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;

        try
        {
            _report = await ApiClient.GetSprintReportAsync(SprintId);
            _burndownData = await ApiClient.GetBurndownDataAsync(SprintId);
        }
        catch
        {
            _report = null;
            _burndownData = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private string BuildBurndownSvg()
    {
        if (_burndownData is null) return string.Empty;

        var data = _burndownData.Points;
        var totalSp = _burndownData.TotalStoryPoints;
        var maxPoints = Math.Max(totalSp, data.Count > 0 ? data.Max(d => d.RemainingStoryPoints) : 0);
        if (maxPoints == 0) maxPoints = 1;

        const int chartWidth = 600;
        const int chartHeight = 250;
        const int padLeft = 45;
        const int padRight = 15;
        const int padTop = 15;
        const int padBottom = 35;
        var plotW = chartWidth - padLeft - padRight;
        var plotH = chartHeight - padTop - padBottom;

        float XPos(int i) => padLeft + (data.Count > 1 ? (float)i / (data.Count - 1) * plotW : plotW / 2f);
        float YPos(int points) => padTop + plotH - (float)points / maxPoints * plotH;

        var sb = new StringBuilder();
        sb.Append($"<svg viewBox=\"0 0 {chartWidth} {chartHeight}\" class=\"tracks-burndown-svg\" preserveAspectRatio=\"xMidYMid meet\">");

        // Grid lines
        var gridSteps = 4;
        for (var g = 0; g <= gridSteps; g++)
        {
            var yGrid = padTop + (float)g / gridSteps * plotH;
            var labelVal = maxPoints - g * maxPoints / gridSteps;
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{padLeft}\" y1=\"{yGrid:F1}\" x2=\"{chartWidth - padRight}\" y2=\"{yGrid:F1}\" stroke=\"var(--color-border, #e0e0e0)\" stroke-width=\"0.5\" />");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{padLeft - 8}\" y=\"{yGrid + 4:F1}\" text-anchor=\"end\" fill=\"var(--color-text-muted, #888)\" font-size=\"11\">{labelVal}</text>");
        }

        // X-axis labels
        var labelInterval = Math.Max(1, data.Count / 7);
        for (var i = 0; i < data.Count; i += labelInterval)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{XPos(i):F1}\" y=\"{chartHeight - 5}\" text-anchor=\"middle\" fill=\"var(--color-text-muted, #888)\" font-size=\"10\">{data[i].Date:M/d}</text>");
        }
        if (data.Count > 1 && (data.Count - 1) % labelInterval != 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{XPos(data.Count - 1):F1}\" y=\"{chartHeight - 5}\" text-anchor=\"middle\" fill=\"var(--color-text-muted, #888)\" font-size=\"10\">{data[^1].Date:M/d}</text>");
        }

        // Ideal line (dashed) — linear from total SP to 0
        for (var i = 0; i < data.Count; i++)
        {
            var idealPoints = data.Count > 1
                ? totalSp - (totalSp * i / (data.Count - 1))
                : 0;
            if (i == 0)
                sb.Append(CultureInfo.InvariantCulture, $"<polyline points=\"");
            sb.Append(CultureInfo.InvariantCulture, $"{XPos(i):F1},{YPos(idealPoints):F1}");
            sb.Append(i < data.Count - 1 ? " " : "\" ");
        }
        sb.Append($"fill=\"none\" stroke=\"var(--color-text-muted, #aaa)\" stroke-width=\"1.5\" stroke-dasharray=\"6,4\" />");

        // Actual line (solid)
        var actualLine = string.Join(" ", data.Select((d, i) => FormattableString.Invariant($"{XPos(i):F1},{YPos(d.RemainingStoryPoints):F1}")));
        sb.Append($"<polyline points=\"{actualLine}\" fill=\"none\" stroke=\"var(--color-primary, #4f8af7)\" stroke-width=\"2.5\" />");

        // Data points
        for (var i = 0; i < data.Count; i++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{XPos(i):F1}\" cy=\"{YPos(data[i].RemainingStoryPoints):F1}\" r=\"3\" fill=\"var(--color-primary, #4f8af7)\" />");
        }

        // Legend
        sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{padLeft + 10}\" y1=\"{padTop + 2}\" x2=\"{padLeft + 30}\" y2=\"{padTop + 2}\" stroke=\"var(--color-text-muted, #aaa)\" stroke-width=\"1.5\" stroke-dasharray=\"6,4\" />");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{padLeft + 34}\" y=\"{padTop + 6}\" fill=\"var(--color-text-muted, #aaa)\" font-size=\"10\">Ideal</text>");
        sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{padLeft + 80}\" y1=\"{padTop + 2}\" x2=\"{padLeft + 100}\" y2=\"{padTop + 2}\" stroke=\"var(--color-primary, #4f8af7)\" stroke-width=\"2.5\" />");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{padLeft + 104}\" y=\"{padTop + 6}\" fill=\"var(--color-primary, #4f8af7)\" font-size=\"10\">Actual</text>");

        sb.Append("</svg>");
        return sb.ToString();
    }
}
