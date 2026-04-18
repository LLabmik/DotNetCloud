using System.Globalization;
using System.Text;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Tracks.UI;

/// <summary>
/// SVG-based velocity chart showing committed vs completed story points across sprints.
/// </summary>
public partial class VelocityChart : ComponentBase
{
    [Inject] private ITracksApiClient ApiClient { get; set; } = default!;

    [Parameter, EditorRequired] public Guid BoardId { get; set; }

    private readonly List<SprintVelocityDto> _velocityData = [];
    private bool _isLoading = true;

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;

        try
        {
            var data = await ApiClient.GetBoardVelocityAsync(BoardId);
            _velocityData.Clear();
            _velocityData.AddRange(data);
        }
        catch
        {
            _velocityData.Clear();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private string BuildVelocitySvg()
    {
        var maxSp = _velocityData.Max(v => Math.Max(v.CommittedPoints, v.CompletedPoints));
        if (maxSp == 0) maxSp = 1;
        var avgLine = _velocityData.Average(v => v.CompletedPoints);

        const int chartWidth = 600;
        const int chartHeight = 220;
        const int padLeft = 45;
        const int padRight = 15;
        const int padTop = 15;
        const int padBottom = 40;
        var plotW = chartWidth - padLeft - padRight;
        var plotH = chartHeight - padTop - padBottom;

        var barGroupWidth = plotW / (float)_velocityData.Count;
        var barWidth = Math.Min(barGroupWidth * 0.35f, 40f);
        const float gapBetween = 3f;

        float YPos(float sp) => padTop + plotH - sp / maxSp * plotH;

        var sb = new StringBuilder();
        sb.Append($"<svg viewBox=\"0 0 {chartWidth} {chartHeight}\" class=\"tracks-velocity-svg\" preserveAspectRatio=\"xMidYMid meet\">");

        // Grid lines
        const int gridSteps = 4;
        for (var g = 0; g <= gridSteps; g++)
        {
            var yGrid = padTop + (float)g / gridSteps * plotH;
            var labelVal = maxSp - g * maxSp / gridSteps;
            sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{padLeft}\" y1=\"{yGrid:F1}\" x2=\"{chartWidth - padRight}\" y2=\"{yGrid:F1}\" stroke=\"var(--color-border, #e0e0e0)\" stroke-width=\"0.5\" />");
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{padLeft - 8}\" y=\"{yGrid + 4:F1}\" text-anchor=\"end\" fill=\"var(--color-text-muted, #888)\" font-size=\"11\">{labelVal}</text>");
        }

        // Average velocity line
        var avgY = YPos((float)avgLine);
        sb.Append(CultureInfo.InvariantCulture, $"<line x1=\"{padLeft}\" y1=\"{avgY:F1}\" x2=\"{chartWidth - padRight}\" y2=\"{avgY:F1}\" stroke=\"var(--color-warning, #f0ad4e)\" stroke-width=\"1.5\" stroke-dasharray=\"6,3\" />");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{chartWidth - padRight - 2}\" y=\"{avgY - 4:F1}\" text-anchor=\"end\" fill=\"var(--color-warning, #f0ad4e)\" font-size=\"10\">Avg {avgLine:F0}</text>");

        // Bar pairs
        for (var i = 0; i < _velocityData.Count; i++)
        {
            var d = _velocityData[i];
            var groupCenter = padLeft + (i + 0.5f) * barGroupWidth;

            // Committed bar
            var committedX = groupCenter - barWidth - gapBetween / 2;
            var committedH = (float)d.CommittedPoints / maxSp * plotH;
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{committedX:F1}\" y=\"{YPos(d.CommittedPoints):F1}\" width=\"{barWidth:F1}\" height=\"{committedH:F1}\" fill=\"var(--color-primary-light, #a3c4f7)\" rx=\"2\" opacity=\"0.6\" />");

            // Completed bar
            var completedX = groupCenter + gapBetween / 2;
            var completedH = (float)d.CompletedPoints / maxSp * plotH;
            sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{completedX:F1}\" y=\"{YPos(d.CompletedPoints):F1}\" width=\"{barWidth:F1}\" height=\"{completedH:F1}\" fill=\"var(--color-primary, #4f8af7)\" rx=\"2\" />");

            // SP labels
            if (d.CommittedPoints > 0)
                sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{committedX + barWidth / 2:F1}\" y=\"{YPos(d.CommittedPoints) - 4:F1}\" text-anchor=\"middle\" fill=\"var(--color-text-muted, #888)\" font-size=\"9\">{d.CommittedPoints}</text>");
            if (d.CompletedPoints > 0)
                sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{completedX + barWidth / 2:F1}\" y=\"{YPos(d.CompletedPoints) - 4:F1}\" text-anchor=\"middle\" fill=\"var(--color-primary, #4f8af7)\" font-size=\"9\">{d.CompletedPoints}</text>");

            // Sprint title label
            var title = System.Net.WebUtility.HtmlEncode(TruncateTitle(d.Title, 10));
            sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{groupCenter:F1}\" y=\"{chartHeight - 5}\" text-anchor=\"middle\" fill=\"var(--color-text-muted, #888)\" font-size=\"10\">{title}</text>");
        }

        // Legend
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{padLeft + 10}\" y=\"{padTop}\" width=\"10\" height=\"10\" fill=\"var(--color-primary-light, #a3c4f7)\" rx=\"1\" opacity=\"0.6\" />");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{padLeft + 24}\" y=\"{padTop + 9}\" fill=\"var(--color-text-muted, #888)\" font-size=\"10\">Committed</text>");
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{padLeft + 90}\" y=\"{padTop}\" width=\"10\" height=\"10\" fill=\"var(--color-primary, #4f8af7)\" rx=\"1\" />");
        sb.Append(CultureInfo.InvariantCulture, $"<text x=\"{padLeft + 104}\" y=\"{padTop + 9}\" fill=\"var(--color-primary, #4f8af7)\" font-size=\"10\">Completed</text>");

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string TruncateTitle(string title, int maxLen)
        => title.Length <= maxLen ? title : string.Concat(title.AsSpan(0, maxLen - 1), "…");
}
