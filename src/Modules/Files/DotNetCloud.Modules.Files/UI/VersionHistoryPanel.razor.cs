using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the version history side panel.
/// </summary>
public partial class VersionHistoryPanel : ComponentBase
{
    /// <summary>Name of the file whose version history is displayed.</summary>
    [Parameter] public string? FileName { get; set; }

    /// <summary>Raised when the panel is closed.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>Raised when the user requests to download a specific version.</summary>
    [Parameter] public EventCallback<FileVersionViewModel> OnDownloadVersion { get; set; }

    /// <summary>Raised when the user requests to restore to a specific version.</summary>
    [Parameter] public EventCallback<FileVersionViewModel> OnRestoreVersion { get; set; }

    /// <summary>Raised when the user requests to delete a specific version.</summary>
    [Parameter] public EventCallback<FileVersionViewModel> OnDeleteVersion { get; set; }

    /// <summary>Raised when the user saves a version label (version, new label).</summary>
    [Parameter] public EventCallback<(Guid VersionId, string Label)> OnLabelSaved { get; set; }

    private List<FileVersionViewModel> _versions = [];
#pragma warning disable CS0649 // Assigned at runtime via future API integration
    private bool _isLoading;
#pragma warning restore CS0649
    private Guid? _editingLabelId;
    private string _editLabelValue = string.Empty;

    /// <summary>Ordered version list (newest first).</summary>
    protected IReadOnlyList<FileVersionViewModel> Versions =>
        _versions.OrderByDescending(v => v.VersionNumber).ToList();

    /// <summary>Whether version data is being fetched.</summary>
    protected bool IsLoading => _isLoading;

    /// <summary>ID of the version whose label is currently being edited, or null.</summary>
    protected Guid? EditingLabelId => _editingLabelId;

    /// <summary>Current value of the label being edited.</summary>
    protected string EditLabelValue
    {
        get => _editLabelValue;
        set => _editLabelValue = value;
    }

    /// <summary>Begins editing the label for the given version.</summary>
    protected void StartEditLabel(FileVersionViewModel version)
    {
        _editingLabelId = version.Id;
        _editLabelValue = version.Label ?? string.Empty;
    }

    /// <summary>Saves the edited label and raises <see cref="OnLabelSaved"/>.</summary>
    protected async Task SaveLabel()
    {
        if (_editingLabelId is null) return;

        var version = _versions.FirstOrDefault(v => v.Id == _editingLabelId);
        if (version is not null)
        {
            version.Label = _editLabelValue.Trim();
            await OnLabelSaved.InvokeAsync((_editingLabelId.Value, version.Label));
        }

        _editingLabelId = null;
        _editLabelValue = string.Empty;
    }

    /// <summary>Cancels the in-progress label edit without saving.</summary>
    protected void CancelEditLabel()
    {
        _editingLabelId = null;
        _editLabelValue = string.Empty;
    }

    /// <summary>Handles keyboard shortcuts in the label input (Enter = save, Escape = cancel).</summary>
    protected async Task HandleLabelKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await SaveLabel();
        if (e.Key == "Escape") CancelEditLabel();
    }

    /// <summary>Raises the download event for the specified version.</summary>
    protected async Task DownloadVersion(FileVersionViewModel version) =>
        await OnDownloadVersion.InvokeAsync(version);

    /// <summary>Restores the file to the specified version and raises the restore event.</summary>
    protected async Task RestoreVersion(FileVersionViewModel version) =>
        await OnRestoreVersion.InvokeAsync(version);

    /// <summary>Deletes the specified version and raises the delete event.</summary>
    protected async Task DeleteVersion(FileVersionViewModel version)
    {
        _versions.Remove(version);
        await OnDeleteVersion.InvokeAsync(version);
    }

    /// <summary>Formats a byte count for display (e.g. "3.2 MB").</summary>
    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
