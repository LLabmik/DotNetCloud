using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the file upload dialog.
/// Uses JavaScript <c>fetch()</c> to upload files directly via HTTP,
/// bypassing Blazor Server's SignalR channel. Supports files up to 16 GB.
/// </summary>
/// <remarks>
/// <para>
/// The browser-side JS module (<c>file-upload.js</c>) reads files using the
/// <c>File.slice()</c> / <c>crypto.subtle.digest()</c> APIs, chunks them into
/// 4 MB pieces, and POSTs each chunk directly to the server's REST API.
/// Progress is reported back via <see cref="DotNetObjectReference{T}"/> callbacks.
/// </para>
/// </remarks>
public partial class FileUploadComponent : ComponentBase, IDisposable
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    /// <summary>Target folder ID for the upload (null = root).</summary>
    [Parameter] public Guid? ParentId { get; set; }

    /// <summary>Invoked when all files have been successfully uploaded.</summary>
    [Parameter] public EventCallback OnUploadComplete { get; set; }

    /// <summary>Invoked when the user cancels or closes the dialog.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    /// <summary>Whether dropped files are pre-loaded in the JS upload module, awaiting display.</summary>
    [Parameter] public bool HasDroppedFiles { get; set; }

    private readonly List<UploadFileItem> _files = [];
    private bool _isDragging;
    private bool _isUploading;
    private string? _errorMessage;
    private DotNetObjectReference<FileUploadComponent>? _jsRef;

    /// <summary>DOM id for the hidden file input element.</summary>
    protected string FileInputId { get; } = $"dnc-upload-input-{Guid.NewGuid():N}";

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && HasDroppedFiles && _files.Count == 0)
        {
            try
            {
                var fileInfos = await JS!.InvokeAsync<FileInfo[]>(
                    "dotnetcloudUpload.getPendingFileInfos");

                if (fileInfos is { Length: > 0 })
                {
                    foreach (var fi in fileInfos)
                    {
                        _files.Add(new UploadFileItem
                        {
                            Name = fi.Name,
                            Size = fi.Size,
                            ContentType = fi.Type,
                            RelativePath = fi.RelativePath
                        });
                    }

                    StateHasChanged();
                }
            }
            catch
            {
                // If JS interop fails during init, user can still browse for files
            }
        }
    }

    /// <summary>Files queued or currently being uploaded.</summary>
    protected IReadOnlyList<UploadFileItem> Files => _files;

    /// <summary>True while the user is dragging files over the drop zone.</summary>
    protected bool IsDragging => _isDragging;

    /// <summary>True while uploads are in progress.</summary>
    protected bool IsUploading => _isUploading;
    protected string? ErrorMessage => _errorMessage;

    /// <summary>Sets the dragging state on drag enter.</summary>
    protected void HandleDragEnter() => _isDragging = true;

    /// <summary>Clears the dragging state on drag leave.</summary>
    protected void HandleDragLeave() => _isDragging = false;

    /// <summary>Dismisses the error alert.</summary>
    protected void DismissError() => _errorMessage = null;

    /// <summary>
    /// Called when the native file input fires its <c>change</c> event.
    /// Invokes JS to register the selected <c>File</c> objects and adds
    /// metadata entries to the Blazor-side queue. No file bytes cross SignalR.
    /// </summary>
    protected async Task HandleFileInputChange()
    {
        if (_isUploading) return;

        _isDragging = false;
        _errorMessage = null;

        try
        {
            var fileInfos = await JS!.InvokeAsync<FileInfo[]>(
                "dotnetcloudUpload.registerFiles", FileInputId);

            if (fileInfos is { Length: > 0 })
            {
                foreach (var fi in fileInfos)
                {
                    _files.Add(new UploadFileItem
                    {
                        Name = fi.Name,
                        Size = fi.Size,
                        ContentType = fi.Type,
                        RelativePath = fi.RelativePath
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to read selected files: {ex.Message}";
        }
    }

    /// <summary>Removes a single pending file from the queue.</summary>
    protected async Task RemoveFile(UploadFileItem file)
    {
        if (_isUploading) return;

        var index = _files.IndexOf(file);
        if (index >= 0)
        {
            _files.RemoveAt(index);
            await JS!.InvokeVoidAsync("dotnetcloudUpload.removeFile", index);
        }
    }

    /// <summary>
    /// Starts uploading all pending files via JS fetch().
    /// Each file is uploaded sequentially; progress is reported per-chunk
    /// back from JS via <see cref="OnJsUploadProgress"/>.
    /// </summary>
    protected async Task StartUpload()
    {
        if (_files.Count == 0) return;

        _errorMessage = null;
        _isUploading = true;
        StateHasChanged();

        await UploadPendingFilesAsync();
    }

    /// <summary>Uploads all pending files, then checks overall state.</summary>
    private async Task UploadPendingFilesAsync()
    {
        try
        {
            var userId = await GetUserIdAsync();
            _jsRef ??= DotNetObjectReference.Create(this);

            var parentIdStr = ParentId?.ToString();

            for (var i = 0; i < _files.Count; i++)
            {
                var file = _files[i];
                if (file.Status != UploadStatus.Pending) continue;

                file.Status = UploadStatus.Uploading;
                file.StatusText = "Starting...";
                StateHasChanged();

                await JS!.InvokeVoidAsync(
                    "dotnetcloudUpload.uploadFile",
                    i, userId, parentIdStr, _jsRef);

                StateHasChanged();
            }

            CheckOverallCompletion();
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            CheckOverallCompletion();
        }
    }

    /// <summary>Evaluates whether all uploads are finished and updates state accordingly.</summary>
    private void CheckOverallCompletion()
    {
        var hasPaused = _files.Any(f => f.Status == UploadStatus.Paused);
        var hasActive = _files.Any(f => f.Status is UploadStatus.Uploading or UploadStatus.Pending);

        if (hasPaused || hasActive)
        {
            // Keep the uploading UI visible while files are paused or active
            return;
        }

        if (_files.All(f => f.Status == UploadStatus.Complete))
        {
            _isUploading = false;
            _ = OnUploadComplete.InvokeAsync();
        }
        else if (_files.Any(f => f.Status == UploadStatus.Failed))
        {
            _errorMessage ??= "One or more files failed to upload.";
            _isUploading = false;
        }
        else
        {
            _isUploading = false;
        }

        StateHasChanged();
    }

    /// <summary>JS callback: per-file progress update.</summary>
    [JSInvokable]
    public void OnJsUploadProgress(int fileIndex, int percent, string statusText)
    {
        ApplyProgress(fileIndex, percent, statusText);
        InvokeAsync(StateHasChanged);
    }

    /// <summary>JS callback: file upload completed successfully.</summary>
    [JSInvokable]
    public void OnJsUploadComplete(int fileIndex)
    {
        ApplyComplete(fileIndex);
        InvokeAsync(async () =>
        {
            StateHasChanged();
            await Task.Delay(1500);
            if (fileIndex >= 0 && fileIndex < _files.Count
                && _files[fileIndex].Status == UploadStatus.Complete)
            {
                _files[fileIndex].DismissedFromView = true;
                StateHasChanged();
            }
        });
    }

    /// <summary>JS callback: file upload failed.</summary>
    [JSInvokable]
    public void OnJsUploadError(int fileIndex, string error)
    {
        ApplyError(fileIndex, error);
        InvokeAsync(StateHasChanged);
    }

    internal void ApplyProgress(int fileIndex, int percent, string statusText)
    {
        if (fileIndex < 0 || fileIndex >= _files.Count) return;
        _files[fileIndex].Progress = percent;
        _files[fileIndex].StatusText = statusText;
    }

    internal void ApplyComplete(int fileIndex)
    {
        if (fileIndex < 0 || fileIndex >= _files.Count) return;
        _files[fileIndex].Progress = 100;
        _files[fileIndex].Status = UploadStatus.Complete;
        _files[fileIndex].StatusText = "Complete";
    }

    internal void ApplyError(int fileIndex, string error)
    {
        if (fileIndex < 0 || fileIndex >= _files.Count) return;
        _files[fileIndex].Status = UploadStatus.Failed;
        _files[fileIndex].StatusText = "Failed";
        _errorMessage = error;
    }

    /// <summary>Removes all files from the queue (only when not uploading).</summary>
    protected async Task ClearFiles()
    {
        if (_isUploading) return;
        _files.Clear();
        await JS!.InvokeVoidAsync("dotnetcloudUpload.clearFiles");
    }

    /// <summary>Pauses an in-progress upload at the current chunk boundary.</summary>
    protected async Task PauseUpload(int fileIndex)
    {
        if (fileIndex < 0 || fileIndex >= _files.Count) return;
        var file = _files[fileIndex];
        if (file.Status != UploadStatus.Uploading) return;

        await JS!.InvokeVoidAsync("dotnetcloudUpload.pauseUpload", fileIndex);
        file.Status = UploadStatus.Paused;
        file.StatusText = "Paused";
        StateHasChanged();
    }

    /// <summary>Resumes a paused upload from where it left off.</summary>
    protected async Task ResumeUpload(int fileIndex)
    {
        if (fileIndex < 0 || fileIndex >= _files.Count) return;
        var file = _files[fileIndex];
        if (file.Status != UploadStatus.Paused) return;

        file.Status = UploadStatus.Uploading;
        file.StatusText = "Resuming...";
        StateHasChanged();

        _jsRef ??= DotNetObjectReference.Create(this);
        var userId = await GetUserIdAsync();
        var parentIdStr = ParentId?.ToString();
        await JS!.InvokeVoidAsync("dotnetcloudUpload.resumeUpload", fileIndex, userId, parentIdStr, _jsRef);

        CheckOverallCompletion();
    }

    /// <summary>Cancels an upload and cleans up the server-side session.</summary>
    protected async Task CancelUpload(int fileIndex)
    {
        if (fileIndex < 0 || fileIndex >= _files.Count) return;
        var file = _files[fileIndex];
        if (file.Status is not (UploadStatus.Uploading or UploadStatus.Paused)) return;

        await JS!.InvokeVoidAsync("dotnetcloudUpload.cancelUpload", fileIndex);
        file.Status = UploadStatus.Failed;
        file.StatusText = "Cancelled";
        CheckOverallCompletion();
    }

    /// <summary>Formats a byte count as a human-readable size string.</summary>
    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    /// <summary>Truncates a filename to keep the UI compact.</summary>
    protected static string TruncateName(string name, int maxLength = 32)
    {
        if (name.Length <= maxLength) return name;
        var ext = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        var budget = maxLength - ext.Length - 1;
        return budget > 0 ? $"{stem[..budget]}…{ext}" : $"{name[..maxLength]}…";
    }

    /// <summary>Returns the CSS modifier for the progress bar fill based on upload status.</summary>
    protected static string GetProgressClass(UploadStatus status) => status switch
    {
        UploadStatus.Complete => "progress-bar-fill--success",
        UploadStatus.Failed => "progress-bar-fill--error",
        UploadStatus.Paused => "progress-bar-fill--paused",
        _ => string.Empty
    };

    private async Task<string> GetUserIdAsync()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = state.User;

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? user.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            throw new InvalidOperationException("Authenticated user id claim is missing.");

        return userIdClaim;
    }

    /// <summary>Disposes the JS interop reference.</summary>
    public void Dispose()
    {
        _jsRef?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>DTO for file metadata returned from JS.</summary>
    internal sealed class FileInfo
    {
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? RelativePath { get; set; }
    }
}
