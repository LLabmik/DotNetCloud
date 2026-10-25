using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Email;
using DotNetCloud.Modules.Email.Models;
using DotNetCloud.Modules.Email.Services;
using DotNetCloud.UI.Shared.Components.DataDisplay;
using DotNetCloud.UI.Shared.Components.Dialogs;
using DotNetCloud.UI.Shared.Components.Forms;
using DotNetCloud.UI.Shared.Components.Navigation;
using DotNetCloud.UI.Shared.Components.Notifications;
using DotNetCloud.UI.Shared.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace DotNetCloud.Modules.Email.UI;

/// <summary>Code-behind for the <see cref="FilePickerModal"/> component.</summary>
public partial class FilePickerModal : ComponentBase
{
    [Parameter, EditorRequired] public required EventCallback<List<ComposeAttachment>> OnFilesSelected { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private List<FileNodeInfo> _files = [];
    private Guid? _currentParentId;
    private string _currentFolderName = "";
    private Guid? _selectedFileId;
    private bool _isLoading = true;
    private bool _isAttaching;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadFilesAsync();
    }

    private async Task LoadFilesAsync()
    {
        if (_fileDirectory is null) return;
        _isLoading = true;
        _errorMessage = null;
        try
        {
            var caller = await GetCallerUserIdAsync();
            if (caller is null) return;
            _files = (await _fileDirectory.ListChildrenAsync(caller.Value, _currentParentId)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list files for file picker");
            _errorMessage = "Failed to load files.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task HandleItemClick(Guid id, string name, bool isFolder)
    {
        if (isFolder)
            await NavigateToFolder(id, name);
        else
            SelectFile(id);
    }

    private async Task NavigateToFolder(Guid folderId, string folderName)
    {
        _currentParentId = folderId;
        _currentFolderName = folderName;
        _selectedFileId = null;
        await LoadFilesAsync();
    }

    private async Task NavigateToRoot()
    {
        _currentParentId = null;
        _currentFolderName = "";
        _selectedFileId = null;
        await LoadFilesAsync();
    }

    private void SelectFile(Guid fileId)
    {
        _selectedFileId = _selectedFileId == fileId ? null : fileId;
    }

    private async Task AttachSelected()
    {
        if (_selectedFileId is null || _fileDirectory is null) return;
        _isAttaching = true;
        try
        {
            var caller = await GetCallerUserIdAsync();
            if (caller is null) return;

            var fileInfo = await _fileDirectory.GetFileInfoAsync(caller.Value, _selectedFileId.Value);
            if (fileInfo is null) return;

            var result = await _apiClient.AttachFromFilesModuleAsync(_selectedFileId.Value);

            var attachment = new ComposeAttachment
            {
                StorageKey = result.StorageKey,
                FileName = result.FileName,
                ContentType = result.ContentType,
                Size = result.Size
            };

            await OnFilesSelected.InvokeAsync([attachment]);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to attach file from Files module");
            _errorMessage = "Failed to attach selected file.";
        }
        finally
        {
            _isAttaching = false;
        }
    }

    private async Task<Guid?> GetCallerUserIdAsync()
    {
        try
        {
            var caller = _currentUserContext.GetCurrentCaller();
            return caller?.UserId;
        }
        catch
        {
            return null;
        }
    }

    private async Task CancelClicked()
    {
        await OnCancel.InvokeAsync();
    }

    private static string GetFileTypeIcon(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType)) return "description";
        var type = mimeType.ToLowerInvariant();
        if (type.StartsWith("image/")) return "image";
        if (type == "application/pdf") return "picture_as_pdf";
        if (type.StartsWith("text/")) return "text_snippet";
        if (type.StartsWith("audio/")) return "music_note";
        if (type.StartsWith("video/")) return "movie";
        if (type.Contains("spreadsheet") || type.Contains("excel") || type.Contains("csv")) return "table_chart";
        if (type.Contains("presentation") || type.Contains("powerpoint") || type.Contains("slides")) return "slideshow";
        if (type.Contains("word") || type.Contains("document")) return "article";
        if (type.Contains("zip") || type.Contains("rar") || type.Contains("gzip") || type.Contains("7z") || type.Contains("tar")) return "folder_zip";
        return "description";
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
