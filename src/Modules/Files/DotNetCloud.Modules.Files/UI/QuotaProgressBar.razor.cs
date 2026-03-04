using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the quota progress bar component.
/// Displays the user's current storage usage as a visual progress bar
/// with colour states for normal, warning (≥80%), critical (≥95%), and exceeded (≥100%).
/// </summary>
public partial class QuotaProgressBar : ComponentBase
{
    /// <summary>Current quota view model. When <see langword="null"/> the component is hidden.</summary>
    [Parameter]
    public QuotaViewModel? Quota { get; set; }

    /// <summary>CSS class applied to the progress bar fill based on current usage.</summary>
    protected string BarCssClass =>
        Quota switch
        {
            { IsExceeded: true } => "quota-bar--exceeded",
            { IsCritical: true } => "quota-bar--critical",
            { IsWarning: true }  => "quota-bar--warning",
            _                    => "quota-bar--normal"
        };

    /// <summary>Bar fill width as a percentage string clamped to 0–100.</summary>
    protected string BarWidthStyle =>
        Quota is null
            ? "width: 0%"
            : $"width: {Math.Min(100.0, Quota.UsagePercent):F1}%";

    /// <summary>Human-readable storage label, e.g. "3.2 GB of 10 GB".</summary>
    protected string QuotaLabel =>
        Quota is null
            ? string.Empty
            : Quota.MaxBytes == 0
                ? $"{FormatBytes(Quota.UsedBytes)} used (unlimited)"
                : $"{FormatBytes(Quota.UsedBytes)} of {FormatBytes(Quota.MaxBytes)}";

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
