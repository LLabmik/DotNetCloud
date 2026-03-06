using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the file upload dialog.
/// Handles drag-drop file selection, chunked upload simulation with speed/ETA tracking,
/// and pause/resume/cancel controls via <see cref="UploadProgressPanel"/>.
/// </summary>
public partial class FileUploadComponent : ComponentBase
{
    [Inject] private IChunkedUploadService UploadService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

    /// <summary>Target folder ID for the upload (null = root).</summary>
    [Parameter] public Guid? ParentId { get; set; }

    /// <summary>Invoked when all files have been successfully uploaded.</summary>
    [Parameter] public EventCallback OnUploadComplete { get; set; }

    /// <summary>Invoked when the user cancels or closes the dialog.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    private readonly List<UploadFileItem> _files = [];
    private bool _isDragging;
    private bool _isUploading;
    private string? _errorMessage;

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
    protected async Task StartUpload()
    {
        _errorMessage = null;
        try
        {
            var caller = await GetCallerContextAsync();

            _isUploading = true;
            StateHasChanged();

            foreach (var file in _files.Where(f => f.Status == UploadStatus.Pending && !f.IsCancelled))
            {
                file.Status = UploadStatus.Uploading;
                StateHasChanged();

                await UploadFileAsync(file, caller);

                StateHasChanged();
            }

            var hasFailures = _files.Any(f => f.Status == UploadStatus.Failed);
            if (hasFailures)
            {
                _errorMessage ??= "One or more files failed to upload. Review failed items and try again.";
                return;
            }

            if (_files.All(f => f.Status == UploadStatus.Complete))
            {
                await OnUploadComplete.InvokeAsync();
            }
        }
        catch (Exception ex)
        {
            _errorMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Upload failed unexpectedly. Please try again."
                : ex.Message;
        }
        finally
        {
            _isUploading = false;
            StateHasChanged();
        }
    }

    private async Task UploadFileAsync(UploadFileItem file, CallerContext caller)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            if (file.IsCancelled)
            {
                file.Status = UploadStatus.Failed;
                file.Progress = 0;
                return;
            }

            while (file.IsPaused)
            {
                file.Status = UploadStatus.Paused;
                StateHasChanged();
                await Task.Delay(200);
            }

            if (file.Status == UploadStatus.Paused)
                file.Status = UploadStatus.Uploading;

            if (file.BrowserFile is null)
            {
                file.Status = UploadStatus.Failed;
                return;
            }

            await using var stream = file.BrowserFile.OpenReadStream(file.Size);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var chunkHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            file.Progress = 20;
            StateHasChanged();

            var session = await UploadService.InitiateUploadAsync(new InitiateUploadDto
            {
                FileName = file.Name,
                ParentId = ParentId,
                TotalSize = file.Size,
                MimeType = file.ContentType,
                ChunkHashes = [chunkHash]
            }, caller);

            file.Progress = 45;
            StateHasChanged();

            if (session.MissingChunks.Contains(chunkHash, StringComparer.OrdinalIgnoreCase))
            {
                await UploadService.UploadChunkAsync(session.SessionId, chunkHash, bytes, caller);
            }

            file.Progress = 80;
            StateHasChanged();

            await UploadService.CompleteUploadAsync(session.SessionId, caller);

            var elapsed = Math.Max(0.001, sw.Elapsed.TotalSeconds);
            file.SpeedBytesPerSecond = file.Size / elapsed;
            file.EtaSeconds = 0;
            file.Progress = 100;
            file.Status = UploadStatus.Complete;
        }
        catch (Exception ex)
        {
            if (!file.IsCancelled)
            {
                file.Status = UploadStatus.Failed;
                _errorMessage = ex.Message;
            }
        }
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

    private async Task<CallerContext> GetCallerContextAsync()
    {
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = state.User;

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing or invalid.");
        }

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        return new CallerContext(userId, roles, CallerType.User);
    }
}
