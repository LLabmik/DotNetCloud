using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the file upload component.
/// </summary>
public partial class FileUploadComponent : ComponentBase
{
    [Parameter] public Guid? ParentId { get; set; }
    [Parameter] public EventCallback OnUploadComplete { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private readonly List<UploadFileItem> _files = [];
    private bool _isDragging;
    private bool _isUploading;

    protected IReadOnlyList<UploadFileItem> Files => _files;
    protected bool IsDragging => _isDragging;
    protected bool IsUploading => _isUploading;

    protected void HandleDragEnter() => _isDragging = true;
    protected void HandleDragLeave() => _isDragging = false;

    protected void HandleFileSelected(InputFileChangeEventArgs e)
    {
        foreach (var file in e.GetMultipleFiles(100))
        {
            _files.Add(new UploadFileItem
            {
                Name = file.Name,
                Size = file.Size,
                ContentType = file.ContentType,
                BrowserFile = file
            });
        }
    }

    protected async Task StartUpload()
    {
        _isUploading = true;

        foreach (var file in _files.Where(f => f.Status == UploadStatus.Pending))
        {
            try
            {
                file.Status = UploadStatus.Uploading;
                StateHasChanged();

                // In a full implementation:
                // 1. Read file into chunks
                // 2. Hash each chunk
                // 3. Call InitiateUpload API
                // 4. Upload missing chunks
                // 5. Call CompleteUpload API
                await Task.Delay(500); // Simulate upload

                file.Progress = 100;
                file.Status = UploadStatus.Complete;
            }
            catch
            {
                file.Status = UploadStatus.Failed;
            }

            StateHasChanged();
        }

        _isUploading = false;

        if (_files.All(f => f.Status == UploadStatus.Complete))
        {
            await OnUploadComplete.InvokeAsync();
        }
    }

    protected void ClearFiles() => _files.Clear();

    protected static string GetStatusLabel(UploadStatus status, int progress)
    {
        return status switch
        {
            UploadStatus.Pending => "Pending",
            UploadStatus.Uploading => $"Uploading {progress}%",
            UploadStatus.Complete => "Complete",
            UploadStatus.Failed => "Failed",
            _ => string.Empty
        };
    }

    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
