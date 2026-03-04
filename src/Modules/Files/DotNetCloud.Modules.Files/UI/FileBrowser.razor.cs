using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the file browser component.
/// </summary>
public partial class FileBrowser : ComponentBase
{
    /// <summary>The current user ID, used for opening the document editor.</summary>
    [Parameter] public Guid UserId { get; set; }

    /// <summary>Base URL for the Files API (e.g., "https://cloud.example.com"), used for the document editor.</summary>
    [Parameter] public string ApiBaseUrl { get; set; } = string.Empty;


    private List<FileNodeViewModel> _nodes = [];
    private readonly List<BreadcrumbItem> _breadcrumbs = [];
    private readonly HashSet<Guid> _selectedNodes = [];
    private Guid? _currentFolderId;
    private ViewMode _viewMode = ViewMode.Grid;
    #pragma warning disable CS0649 // Fields assigned at runtime via future API integration
    private bool _isLoading;
#pragma warning restore CS0649
    private bool _showCreateFolder;
    private bool _showUploadDialog;
    private bool _showShareDialog;
    private bool _showPreview;
    private bool _showDocumentEditor;
    private string _newFolderName = string.Empty;
    private FileNodeViewModel? _shareTargetNode;
    private FileNodeViewModel? _previewNode;
    private FileNodeViewModel? _editorNode;
    private int _currentPage = 1;
    private int _pageSize = 50;
#pragma warning disable CS0649
    private int _totalCount;
#pragma warning restore CS0649
    private QuotaViewModel? _quota;

    protected IReadOnlyList<FileNodeViewModel> Nodes => _nodes;
    protected IReadOnlyList<BreadcrumbItem> Breadcrumbs => _breadcrumbs;
    protected int SelectedCount => _selectedNodes.Count;
    protected Guid? CurrentFolderId => _currentFolderId;
    protected bool IsLoading => _isLoading;
    protected QuotaViewModel? Quota => _quota;
    protected bool IsShowCreateFolder => _showCreateFolder;
    protected bool IsShowUploadDialog => _showUploadDialog;
    protected bool IsShowShareDialog => _showShareDialog;
    protected bool IsShowPreview => _showPreview;
    protected bool IsShowDocumentEditor => _showDocumentEditor;
    protected string NewFolderName { get => _newFolderName; set => _newFolderName = value; }
    protected FileNodeViewModel? ShareTargetNode => _shareTargetNode;
    protected FileNodeViewModel? PreviewNode => _previewNode;
    protected FileNodeViewModel? EditorNode => _editorNode;
    protected int CurrentPage => _currentPage;
    protected int PageSize => _pageSize;
    protected int TotalCount => _totalCount;
    protected int TotalPages => _totalCount > 0 ? (int)Math.Ceiling((double)_totalCount / _pageSize) : 1;

    /// <summary>Updates the quota display with fresh data from the API.</summary>
    protected void UpdateQuota(long usedBytes, long maxBytes, double usagePercent)
    {
        _quota = new QuotaViewModel
        {
            UsedBytes = usedBytes,
            MaxBytes = maxBytes,
            UsagePercent = usagePercent
        };
    }

    protected void NavigateToFolder(Guid? folderId)
    {
        _currentFolderId = folderId;
        _currentPage = 1;
        _selectedNodes.Clear();

        if (folderId is null)
        {
            _breadcrumbs.Clear();
        }

        _nodes = [];
    }

    protected void HandleNodeClick(FileNodeViewModel node)
    {
        if (_selectedNodes.Contains(node.Id))
            _selectedNodes.Remove(node.Id);
        else
            _selectedNodes.Add(node.Id);
    }

    protected void HandleNodeDoubleClick(FileNodeViewModel node)
    {
        if (node.NodeType == "Folder")
        {
            _breadcrumbs.Add(new BreadcrumbItem(node.Id, node.Name));
            NavigateToFolder(node.Id);
        }
        else if (DocumentEditor.IsSupportedForEditing(node.Name))
        {
            ShowDocumentEditor(node);
        }
        else
        {
            ShowPreview(node);
        }
    }

    protected bool IsSelected(Guid nodeId) => _selectedNodes.Contains(nodeId);
    protected void ClearSelection() => _selectedNodes.Clear();

    protected void ShowCreateFolderDialog()
    {
        _showCreateFolder = true;
        _newFolderName = string.Empty;
    }

    protected void HideCreateFolder() => _showCreateFolder = false;

    protected void CreateFolder()
    {
        if (string.IsNullOrWhiteSpace(_newFolderName)) return;
        _showCreateFolder = false;
        _newFolderName = string.Empty;
    }

    protected void HandleFolderKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") CreateFolder();
        if (e.Key == "Escape") _showCreateFolder = false;
    }

    protected void ShowUploadDialog() => _showUploadDialog = true;
    protected void HideUploadDialog() => _showUploadDialog = false;
    protected void HandleUploadComplete() => _showUploadDialog = false;

    protected void DeleteSelected() => _selectedNodes.Clear();

    protected void ToggleFavorite(Guid nodeId)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is not null)
        {
            node.IsFavorite = !node.IsFavorite;
        }
    }

    protected void ShowShareDialog(FileNodeViewModel node)
    {
        _shareTargetNode = node;
        _showShareDialog = true;
    }

    protected void HideShareDialog() => _showShareDialog = false;

    protected void ShowPreview(FileNodeViewModel node)
    {
        _previewNode = node;
        _showPreview = true;
    }

    protected void HidePreview() => _showPreview = false;

    protected void ShowDocumentEditor(FileNodeViewModel node)
    {
        _editorNode = node;
        _showDocumentEditor = true;
    }

    protected void HideDocumentEditor()
    {
        _showDocumentEditor = false;
        _editorNode = null;
    }

    protected void ToggleViewMode()
    {
        _viewMode = _viewMode == ViewMode.Grid ? ViewMode.List : ViewMode.Grid;
    }

    protected void PreviousPage() { if (_currentPage > 1) _currentPage--; }
    protected void NextPage() { if (_currentPage < TotalPages) _currentPage++; }

    protected string GetViewToggleLabel() =>
        _viewMode == ViewMode.Grid ? "List" : "Grid";

    protected string GetFilesContainerClass() =>
        _viewMode == ViewMode.Grid ? "files-grid" : "files-list";

    protected static string GetNodeIcon(FileNodeViewModel node)
    {
        if (node.NodeType == "Folder") return "[Folder]";
        return GetFileIcon(node.MimeType);
    }

    protected static string GetFileIcon(string? mimeType)
    {
        if (mimeType is null) return "[File]";
        if (mimeType.StartsWith("image/")) return "[Image]";
        if (mimeType.StartsWith("video/")) return "[Video]";
        if (mimeType.StartsWith("audio/")) return "[Audio]";
        if (mimeType.StartsWith("text/")) return "[Text]";
        if (mimeType == "application/pdf") return "[PDF]";
        if (mimeType.Contains("spreadsheet") || mimeType.Contains("excel")) return "[Sheet]";
        if (mimeType.Contains("presentation") || mimeType.Contains("powerpoint")) return "[Slides]";
        if (mimeType.Contains("document") || mimeType.Contains("word")) return "[Doc]";
        if (mimeType.Contains("zip") || mimeType.Contains("compressed")) return "[Archive]";
        return "[File]";
    }

    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }
}
