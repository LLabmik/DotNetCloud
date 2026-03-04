using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the file upload dialog.
/// Handles drag-drop file selection, chunked upload simulation with speed/ETA tracking,
/// and pause/resume/cancel controls via <see cref="UploadProgressPanel"/>.
/// </summary>
public partial class FileUploadComponent : ComponentBase
{
    /// <summary>Target folder ID for the upload (null = root).</summary>
    [Parameter] public Guid? ParentId { get; set; }

    /// <summary>Invoked when all files have been successfully uploaded.</summary>
    [Parameter] public EventCallback OnUploadComplete { get; set; }

    /// <summary>Invoked when the user cancels or closes the dialog.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    private readonly List<UploadFileItem> _files = [];
    private bool _isDragging;
    private bool _isUploading;

    /// <summary>Files queued or currently being uploaded.</summary>
    protected IReadOnlyList<UploadFileItem> Files => _files;

    /// <summary>True while the user is dragging files over the drop zone.</summary>
    protected bool IsDragging => _isDragging;

    /// <summary>True while uploads are in progress.</summary>
    protected bool IsUploading => _isUploading;

    /// <summary>Sets the dragging state on drag enter.</summary>
    protected void HandleDragEnter() => _isDragging = true;

    /// <summary>Clears the dragging state on drag leave.</summary>
    protected void HandleDragLeave() => _isDragging = false;

    /// <summary>Adds selected files to the upload queue.</summary>
    protected void HandleFileSelected(InputFileChangeEventArgs e)
    {
        _isDragging = false;
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

    /// <summary>Removes a single pending file from the queue.</summary>
    protected void RemoveFile(UploadFileItem file)
    {
        if (!_isUploading)
            _files.Remove(file);
    }

    /// <summary>
    /// Starts uploading all pending files sequentially.
    /// Tracks per-file speed and ETA; honours pause and cancel flags.
    /// </summary>
    /// <remarks>
    /// The actual upload uses chunked upload via <c>IChunkedUploadService</c>.
    /// This stub simulates progress with <c>Task.Delay</c> until the API client is wired.
    /// </remarks>
    protected async Task StartUpload()
    {
        _isUploading = true;
        StateHasChanged();

        foreach (var file in _files.Where(f => f.Status == UploadStatus.Pending && !f.IsCancelled))
        {
            file.Status = UploadStatus.Uploading;
            StateHasChanged();

            await UploadFileAsync(file);

            StateHasChanged();
        }

        _isUploading = false;

        if (_files.All(f => f.Status == UploadStatus.Complete))
            await OnUploadComplete.InvokeAsync();
    }

    private async Task UploadFileAsync(UploadFileItem file)
    {
        const int simulatedChunks = 10;
        var sw = Stopwatch.StartNew();
        var bytesPerChunk = Math.Max(1, file.Size / simulatedChunks);

        for (var chunk = 0; chunk < simulatedChunks; chunk++)
        {
            // Honour cancel
            if (file.IsCancelled)
            {
                file.Status = UploadStatus.Failed;
                file.Progress = 0;
                return;
            }

            // Wait while paused (poll every 200 ms)
            while (file.IsPaused)
            {
                file.Status = UploadStatus.Paused;
                StateHasChanged();
                await Task.Delay(200);
            }

            if (file.Status == UploadStatus.Paused)
                file.Status = UploadStatus.Uploading;

            try
            {
                // In a full implementation:
                // 1. Hash the chunk bytes (SHA-256)
                // 2. Call IChunkedUploadService.InitiateUploadAsync (first chunk only)
                // 3. Call IChunkedUploadService.UploadChunkAsync
                // 4. Call IChunkedUploadService.CompleteUploadAsync (last chunk only)
                await Task.Delay(300); // Simulate network I/O

                var chunkBytes = (long)(chunk + 1) * bytesPerChunk;
                file.Progress = (int)Math.Min(100, (chunkBytes * 100) / Math.Max(1, file.Size));

                var elapsed = sw.Elapsed.TotalSeconds;
                if (elapsed > 0)
                {
                    var bytesUploaded = Math.Min(file.Size, (long)(chunk + 1) * bytesPerChunk);
                    file.SpeedBytesPerSecond = bytesUploaded / elapsed;
                    var remaining = file.Size - bytesUploaded;
                    file.EtaSeconds = file.SpeedBytesPerSecond > 0
                        ? remaining / file.SpeedBytesPerSecond
                        : null;
                }

                StateHasChanged();
            }
            catch
            {
                file.Status = UploadStatus.Failed;
                return;
            }
        }

        file.Progress = 100;
        file.SpeedBytesPerSecond = 0;
        file.EtaSeconds = null;
        file.Status = UploadStatus.Complete;
    }

    /// <summary>Marks the file as paused; the upload loop will wait.</summary>
    protected void PauseUpload(UploadFileItem file)
    {
        if (file.Status == UploadStatus.Uploading)
            file.IsPaused = true;
    }

    /// <summary>Clears the paused flag so the upload loop resumes.</summary>
    protected void ResumeUpload(UploadFileItem file)
    {
        if (file.IsPaused)
        {
            file.IsPaused = false;
            file.Status = UploadStatus.Uploading;
        }
    }

    /// <summary>Marks a file as cancelled; it will be skipped on the next loop iteration.</summary>
    protected void CancelUpload(UploadFileItem file)
    {
        file.IsCancelled = true;
        file.IsPaused = false;
    }

    /// <summary>Removes all files from the queue (only when not uploading).</summary>
    protected void ClearFiles()
    {
        if (!_isUploading)
            _files.Clear();
    }

    /// <summary>Formats a byte count as a human-readable size string.</summary>
    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
