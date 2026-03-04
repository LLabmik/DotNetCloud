using Microsoft.AspNetCore.Components;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the floating upload progress panel.
/// Displays per-file progress with pause/resume/cancel controls and an aggregate progress bar.
/// </summary>
public partial class UploadProgressPanel : ComponentBase
{
    /// <summary>The list of files currently being tracked for upload.</summary>
    [Parameter] public IReadOnlyList<UploadFileItem> Files { get; set; } = [];

    /// <summary>Invoked when the user requests to pause a specific file upload.</summary>
    [Parameter] public EventCallback<UploadFileItem> OnPause { get; set; }

    /// <summary>Invoked when the user requests to resume a paused file upload.</summary>
    [Parameter] public EventCallback<UploadFileItem> OnResume { get; set; }

    /// <summary>Invoked when the user requests to cancel a file upload.</summary>
    [Parameter] public EventCallback<UploadFileItem> OnCancel { get; set; }

    /// <summary>Invoked when the user closes or dismisses the progress panel.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    private bool _isMinimized;

    /// <summary>Whether the panel body is collapsed to show only the header.</summary>
    protected bool IsMinimized => _isMinimized;

    /// <summary>Number of files currently pending, uploading, or paused.</summary>
    protected int ActiveCount =>
        Files.Count(f => f.Status is UploadStatus.Pending or UploadStatus.Uploading or UploadStatus.Paused);

    /// <summary>Aggregate upload progress (0–100) across all files.</summary>
    protected int OverallProgress =>
        Files.Count == 0 ? 0 : (int)Files.Average(f => f.Progress);

    /// <summary>Toggles the minimised/expanded state of the panel.</summary>
    protected void ToggleMinimize() => _isMinimized = !_isMinimized;

    /// <summary>Raises <see cref="OnClose"/>.</summary>
    protected async Task ClosePanel() => await OnClose.InvokeAsync();

    /// <summary>Raises <see cref="OnPause"/> for the given file.</summary>
    protected async Task PauseFile(UploadFileItem file) => await OnPause.InvokeAsync(file);

    /// <summary>Raises <see cref="OnResume"/> for the given file.</summary>
    protected async Task ResumeFile(UploadFileItem file) => await OnResume.InvokeAsync(file);

    /// <summary>Raises <see cref="OnCancel"/> for the given file.</summary>
    protected async Task CancelFile(UploadFileItem file) => await OnCancel.InvokeAsync(file);

    /// <summary>Returns the CSS modifier for the progress bar fill based on upload status.</summary>
    protected static string GetProgressClass(UploadStatus status) => status switch
    {
        UploadStatus.Complete => "progress-bar-fill--success",
        UploadStatus.Failed   => "progress-bar-fill--error",
        UploadStatus.Paused   => "progress-bar-fill--paused",
        _                     => string.Empty
    };

    /// <summary>Returns a human-readable status string for a file upload item.</summary>
    protected static string GetStatusLabel(UploadFileItem file) => file.Status switch
    {
        UploadStatus.Pending   => "Pending",
        UploadStatus.Uploading => BuildUploadingLabel(file),
        UploadStatus.Paused    => "Paused",
        UploadStatus.Complete  => "Complete",
        UploadStatus.Failed    => "Failed",
        _                      => string.Empty
    };

    /// <summary>Truncates a filename to keep the panel compact.</summary>
    protected static string TruncateName(string name, int maxLength = 28)
    {
        if (name.Length <= maxLength) return name;
        var ext = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        var budget = maxLength - ext.Length - 1;
        return budget > 0 ? $"{stem[..budget]}…{ext}" : $"{name[..maxLength]}…";
    }

    private static string BuildUploadingLabel(UploadFileItem file)
    {
        var parts = new List<string> { $"{file.Progress}%" };

        if (file.SpeedBytesPerSecond > 0)
            parts.Add(FormatSpeed(file.SpeedBytesPerSecond));

        if (file.EtaSeconds is > 0)
            parts.Add(FormatEta(file.EtaSeconds.Value));

        return string.Join(" · ", parts);
    }

    private static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec < 1024) return $"{bytesPerSec:F0} B/s";
        if (bytesPerSec < 1024 * 1024) return $"{bytesPerSec / 1024.0:F1} KB/s";
        return $"{bytesPerSec / (1024.0 * 1024.0):F1} MB/s";
    }

    private static string FormatEta(double seconds)
    {
        if (seconds < 60) return $"{(int)seconds}s left";
        if (seconds < 3600) return $"{(int)(seconds / 60)}m left";
        return $"{(int)(seconds / 3600)}h left";
    }
}
