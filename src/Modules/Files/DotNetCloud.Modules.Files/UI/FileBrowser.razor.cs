using System.Security.Claims;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.UI;

/// <summary>
/// Code-behind for the file browser component.
/// Handles file/folder listing, shared-with-me/shared-by-me views,
/// upload, preview, sharing, and document editing.
/// </summary>
public partial class FileBrowser : ComponentBase, IAsyncDisposable
{
    [Inject] private IFileService FileService { get; set; } = default!;
    [Inject] private IChunkedUploadService UploadService { get; set; } = default!;
    [Inject] private ICollaboraDiscoveryService CollaboraDiscoveryService { get; set; } = default!;
    [Inject] private IOptions<CollaboraOptions> CollaboraOptions { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>The current user ID, used for opening the document editor.</summary>
    [Parameter] public Guid UserId { get; set; }

    /// <summary>Base URL for the Files API (e.g., "https://cloud.example.com"), used for the document editor.</summary>
    [Parameter] public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>The currently active sidebar section, controlling which view is rendered.</summary>
    [Parameter] public FileSidebarSection ActiveSection { get; set; } = FileSidebarSection.AllFiles;

    /// <summary>Items shared with the current user (for the SharedWithMe view).</summary>
    [Parameter] public IReadOnlyList<SharedItemViewModel> SharedWithMeItems { get; set; } = [];

    /// <summary>Items shared by the current user (for the SharedByMe view).</summary>
    [Parameter] public IReadOnlyList<SharedItemViewModel> SharedByMeItems { get; set; } = [];

    /// <summary>Whether shared items are being loaded.</summary>
    [Parameter] public bool IsLoadingSharedItems { get; set; }

    /// <summary>Raised when the user opens a shared item.</summary>
    [Parameter] public EventCallback<SharedItemViewModel> OnOpenSharedItem { get; set; }

    /// <summary>Raised when the user declines a share from "Shared with me".</summary>
    [Parameter] public EventCallback<SharedItemViewModel> OnDeclineShare { get; set; }

    /// <summary>Raised when the user wants to manage a share from "Shared by me".</summary>
    [Parameter] public EventCallback<SharedItemViewModel> OnManageShare { get; set; }

    /// <summary>Raised when the user revokes a share from "Shared by me".</summary>
    [Parameter] public EventCallback<SharedItemViewModel> OnRevokeShare { get; set; }

    /// <summary>Raised when a share permission is changed inline from "Shared by me".</summary>
    [Parameter] public EventCallback<SharePermissionChangedEventArgs> OnSharePermissionChanged { get; set; }

    /// <summary>Raised when the user copies a public link from "Shared by me".</summary>
    [Parameter] public EventCallback<SharedItemViewModel> OnCopyShareLink { get; set; }

    /// <summary>
    /// Raised when the user adds a tag to all selected nodes.
    /// Arguments: (nodeIds, tagName, color).
    /// </summary>
    [Parameter] public EventCallback<(IReadOnlyList<Guid> NodeIds, string TagName, string? Color)> OnBulkTagAdd { get; set; }

    private List<FileNodeViewModel> _nodes = [];
    private readonly List<BreadcrumbItem> _breadcrumbs = [];
    private readonly HashSet<Guid> _selectedNodes = [];
    private Guid? _currentFolderId;
    private ViewMode _viewMode = ViewMode.Grid;
    private string _sortColumn = "Name";
    private bool _sortAscending = true;
    #pragma warning disable CS0649 // Fields assigned at runtime via future API integration
    private bool _isLoading;
#pragma warning restore CS0649
    private bool _showCreateFolder;
    private bool _showUploadDialog;
    private bool _showShareDialog;
    private bool _showPreview;
    private bool _showDocumentEditor;
    private bool _showCreateDocument;
    private bool _showBulkTagAdd;
    private string _newFolderName = string.Empty;
    private string _newDocumentName = "Untitled";
    private string _selectedDocumentExtension = "docx";
    private bool _isCollaboraAvailable;
    private bool _isCollaboraConfigured;
    private HashSet<string> _collaboraEditableExtensions = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _supportedNewDocumentExtensions = [];
    private FileNodeViewModel? _shareTargetNode;
    private FileNodeViewModel? _previewNode;
    private FileNodeViewModel? _editorNode;
    private int _currentPage = 1;
    private int _pageSize = 50;
#pragma warning disable CS0649
    private int _totalCount;
#pragma warning restore CS0649
    private QuotaViewModel? _quota;
    private FileTagViewModel? _activeTag;
    private List<FileNodeViewModel> _taggedNodes = [];
    private List<FileTagViewModel> _userTags = [];

    // Drag-and-drop: use a counter to handle bubbling (child elements fire enter/leave too).
    private int _dragEnterCount;
    private readonly string _browserDropInputId = $"files-drop-input-{Guid.NewGuid():N}";
    private List<IBrowserFile> _droppedFiles = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadCollaboraCapabilitiesAsync();
        await LoadCurrentFolderAsync();
    }

    protected IReadOnlyList<FileNodeViewModel> Nodes => _nodes;

    /// <summary>Nodes sorted by the current sort column and direction (folders always first).</summary>
    protected IReadOnlyList<FileNodeViewModel> SortedNodes
    {
        get
        {
            IEnumerable<FileNodeViewModel> ordered = (_sortColumn, _sortAscending) switch
            {
                ("Name", true)  => _nodes.OrderBy(n => n.NodeType != "Folder").ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase),
                ("Name", false) => _nodes.OrderBy(n => n.NodeType != "Folder").ThenByDescending(n => n.Name, StringComparer.OrdinalIgnoreCase),
                ("Size", true)  => _nodes.OrderBy(n => n.NodeType != "Folder").ThenBy(n => n.Size),
                ("Size", false) => _nodes.OrderBy(n => n.NodeType != "Folder").ThenByDescending(n => n.Size),
                ("Date", true)  => _nodes.OrderBy(n => n.NodeType != "Folder").ThenBy(n => n.UpdatedAt),
                ("Date", false) => _nodes.OrderBy(n => n.NodeType != "Folder").ThenByDescending(n => n.UpdatedAt),
                ("Type", true)  => _nodes.OrderBy(n => n.NodeType != "Folder").ThenBy(n => n.MimeType),
                _               => _nodes.OrderBy(n => n.NodeType != "Folder").ThenByDescending(n => n.MimeType),
            };
            return [.. ordered];
        }
    }
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
    protected bool IsShowCreateDocument => _showCreateDocument;
    protected bool IsCollaboraAvailable => _isCollaboraAvailable;
    protected bool CanCreateCollaboraDocument => _isCollaboraConfigured && _supportedNewDocumentExtensions.Count > 0;
    protected IReadOnlyList<string> SupportedNewDocumentExtensions => _supportedNewDocumentExtensions;
    protected bool IsShowBulkTagAdd => _showBulkTagAdd;
    protected FileTagViewModel? ActiveTag => _activeTag;
    protected IReadOnlyList<FileNodeViewModel> TaggedNodes => _taggedNodes;
    protected IReadOnlyList<FileTagViewModel> UserTags => _userTags;
    protected string NewFolderName { get => _newFolderName; set => _newFolderName = value; }
    protected string NewDocumentName { get => _newDocumentName; set => _newDocumentName = value; }
    protected string SelectedDocumentExtension { get => _selectedDocumentExtension; set => _selectedDocumentExtension = value; }
    protected FileNodeViewModel? ShareTargetNode => _shareTargetNode;
    protected FileNodeViewModel? PreviewNode => _previewNode;
    protected FileNodeViewModel? EditorNode => _editorNode;
    protected int CurrentPage => _currentPage;
    protected int PageSize => _pageSize;
    protected int TotalCount => _totalCount;
    protected int TotalPages => _totalCount > 0 ? (int)Math.Ceiling((double)_totalCount / _pageSize) : 1;

    /// <summary>True while the user is dragging files over the browser area.</summary>
    protected bool IsBrowserDragging => _dragEnterCount > 0;
    protected string BrowserDropInputId => _browserDropInputId;
    protected IReadOnlyList<IBrowserFile> DroppedFiles => _droppedFiles;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The bridge is idempotent and can be called multiple times safely.
        await Js.InvokeVoidAsync("dotnetcloudFilesDrop.init", ".files-browser", $"#{_browserDropInputId}");
    }

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

        _ = LoadCurrentFolderAsync();
    }

    protected void HandleNodeClick(FileNodeViewModel node)
    {
        if (_selectedNodes.Contains(node.Id))
            _selectedNodes.Remove(node.Id);
        else
            _selectedNodes.Add(node.Id);
    }

    protected async Task HandleNodeDoubleClick(FileNodeViewModel node)
    {
        if (node.NodeType == "Folder")
        {
            _breadcrumbs.Add(new BreadcrumbItem(node.Id, node.Name));
            NavigateToFolder(node.Id);
        }
        else if (CanOpenInDocumentEditor(node))
        {
            ShowDocumentEditor(node);
        }
        else
        {
            ShowPreview(node);
        }

        await Task.CompletedTask;
    }

    protected void OpenFolder(FileNodeViewModel node)
    {
        if (node.NodeType != "Folder")
        {
            return;
        }

        _breadcrumbs.Add(new BreadcrumbItem(node.Id, node.Name));
        NavigateToFolder(node.Id);
    }
    
        protected async Task OpenNodeAsync(FileNodeViewModel node)
        {
            await HandleNodeDoubleClick(node);
        }

    protected bool IsSelected(Guid nodeId) => _selectedNodes.Contains(nodeId);
    protected void ClearSelection() => _selectedNodes.Clear();

    protected void ShowCreateFolderDialog()
    {
        _showCreateFolder = true;
        _newFolderName = string.Empty;
    }

    protected void HideCreateFolder() => _showCreateFolder = false;

    protected async Task CreateFolder()
    {
        if (string.IsNullOrWhiteSpace(_newFolderName)) return;

        var caller = await GetCallerContextAsync();
        var createDto = new CreateFolderDto
        {
            Name = _newFolderName.Trim(),
            ParentId = _currentFolderId
        };

        await FileService.CreateFolderAsync(createDto, caller);
        _showCreateFolder = false;
        _newFolderName = string.Empty;
        await LoadCurrentFolderAsync();
    }

    protected async Task HandleFolderKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await CreateFolder();
        if (e.Key == "Escape") _showCreateFolder = false;
    }

    protected void ShowUploadDialog() => _showUploadDialog = true;
    protected void HideUploadDialog()
    {
        _showUploadDialog = false;
        _droppedFiles.Clear();
    }

    protected void ShowCreateDocumentDialog()
    {
        if (!CanCreateCollaboraDocument)
            return;

        _showCreateDocument = true;
        _newDocumentName = "Untitled";

        if (!_supportedNewDocumentExtensions.Contains(_selectedDocumentExtension, StringComparer.OrdinalIgnoreCase))
            _selectedDocumentExtension = _supportedNewDocumentExtensions[0];
    }

    protected void HideCreateDocumentDialog() => _showCreateDocument = false;

    protected async Task HandleCreateDocumentKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await CreateDocumentAsync();

        if (e.Key == "Escape")
            _showCreateDocument = false;
    }

    protected async Task CreateDocumentAsync()
    {
        if (!CanCreateCollaboraDocument)
            return;

        var caller = await GetCallerContextAsync();
        var extension = NormalizeExtension(_selectedDocumentExtension);
        var requestedName = string.IsNullOrWhiteSpace(_newDocumentName) ? "Untitled" : _newDocumentName.Trim();
        var fileName = EnsureUniqueFileName(BuildFileName(requestedName, extension));

        var session = await UploadService.InitiateUploadAsync(new InitiateUploadDto
        {
            FileName = fileName,
            ParentId = _currentFolderId,
            TotalSize = 0,
            MimeType = GetMimeType(extension),
            ChunkHashes = []
        }, caller);

        var created = await UploadService.CompleteUploadAsync(session.SessionId, caller);

        _showCreateDocument = false;
        await LoadCurrentFolderAsync();

        if (CanOpenInDocumentEditor(created.Name))
        {
            ShowDocumentEditor(ToViewModel(created));
        }
    }

    protected async Task HandleUploadComplete()
    {
        _showUploadDialog = false;
        _droppedFiles.Clear();
        await LoadCurrentFolderAsync();
    }

    // ── Drag-and-drop zone (browser-level) ─────────────────────────────────────

    /// <summary>
    /// Increments the drag counter when a drag enters the browser area.
    /// Using a counter instead of a bool prevents flicker caused by enter/leave
    /// events from child elements.
    /// </summary>
    protected void HandleBrowserDragEnter() => _dragEnterCount++;

    /// <summary>Decrements the drag counter when a drag leaves the browser area.</summary>
    protected void HandleBrowserDragLeave()
    {
        if (_dragEnterCount > 0) _dragEnterCount--;
    }

    /// <summary>Resets drag state when files are dropped; JS bridge handles file transfer to hidden input.</summary>
    protected void HandleBrowserDrop() => _dragEnterCount = 0;

    /// <summary>Receives dropped files via hidden browser-level InputFile and opens upload dialog pre-populated.</summary>
    protected void HandleBrowserFileDrop(InputFileChangeEventArgs e)
    {
        _dragEnterCount = 0;
        _droppedFiles = [.. e.GetMultipleFiles(100)];
        _showUploadDialog = true;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    protected async Task DeleteSelected()
    {
        if (_selectedNodes.Count == 0)
        {
            return;
        }

        var caller = await GetCallerContextAsync();
        var nodeIds = _selectedNodes.ToList();

        foreach (var nodeId in nodeIds)
        {
            await FileService.DeleteAsync(nodeId, caller);
        }

        _selectedNodes.Clear();
        await LoadCurrentFolderAsync();
    }

    /// <summary>Activates the tag filter view for the given tag.</summary>
    protected void FilterByTag(FileTagViewModel tag)
    {
        _activeTag = tag;
        _taggedNodes = [];
    }

    /// <summary>Clears the active tag filter.</summary>
    protected void ClearTagFilter()
    {
        _activeTag = null;
        _taggedNodes = [];
    }

    /// <summary>Updates the user tag list (for sidebar and autocomplete).</summary>
    protected void SetUserTags(IReadOnlyList<FileTagViewModel> tags)
    {
        _userTags = [.. tags];
    }

    /// <summary>Updates the tagged-nodes list for the current tag filter.</summary>
    protected void SetTaggedNodes(IReadOnlyList<FileNodeViewModel> nodes)
    {
        _taggedNodes = [.. nodes];
    }

    /// <summary>Shows the bulk-tag-add panel.</summary>
    protected void ShowBulkTagAdd() => _showBulkTagAdd = true;

    /// <summary>Hides the bulk-tag-add panel.</summary>
    protected void HideBulkTagAdd() => _showBulkTagAdd = false;

    /// <summary>
    /// Called when the user confirms adding a tag via the bulk-tag panel.
    /// Raises <see cref="OnBulkTagAdd"/> so the host page can call the API.
    /// </summary>
    protected async Task HandleBulkTagAdd((string Name, string? Color) tag)
    {
        _showBulkTagAdd = false;
        await OnBulkTagAdd.InvokeAsync((SelectedNodeIds, tag.Name, tag.Color));
    }

    /// <summary>Returns the IDs of all currently selected nodes.</summary>
    protected IReadOnlyList<Guid> SelectedNodeIds => [.. _selectedNodes];

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

    /// <summary>Closes the preview and opens the share dialog for the given node.</summary>
    protected void HandlePreviewShare(FileNodeViewModel node)
    {
        HidePreview();
        ShowShareDialog(node);
    }

    /// <summary>Closes the preview and triggers a download for the given node.</summary>
    protected async Task HandlePreviewDownload(FileNodeViewModel node)
    {
        HidePreview();
        await DownloadNodeAsync(node);
    }

    /// <summary>Closes the editor and triggers file download for the current editor node.</summary>
    protected async Task HandleEditorDownload()
    {
        var node = _editorNode;
        HideDocumentEditor();

        if (node is not null)
        {
            await DownloadNodeAsync(node);
        }
    }

    protected void ShowDocumentEditor(FileNodeViewModel node)
    {
        _editorNode = node;
        _showDocumentEditor = true;
    }

    protected bool CanOpenInDocumentEditor(FileNodeViewModel node) =>
        node.NodeType == "File" && CanOpenInDocumentEditor(node.Name);

    private bool CanOpenInDocumentEditor(string fileName)
    {
        if (!_isCollaboraConfigured)
            return false;

        var extension = NormalizeExtension(Path.GetExtension(fileName));
        return !string.IsNullOrWhiteSpace(extension) && _collaboraEditableExtensions.Contains(extension);
    }

    protected void HideDocumentEditor()
    {
        _showDocumentEditor = false;
        _editorNode = null;
    }

    /// <summary>Sets the active sort column; toggles direction when already active.</summary>
    protected void SetSort(string column)
    {
        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn = column;
            _sortAscending = column != "Date";
        }
    }

    /// <summary>Returns the CSS class for a list-view sort header (active/inactive).</summary>
    protected string SortHeaderClass(string column) =>
        _sortColumn == column ? "sort-header--active" : string.Empty;

    /// <summary>Returns the sort direction indicator (▲/▼) for a column header.</summary>
    protected string SortIndicator(string column) =>
        _sortColumn != column ? string.Empty : _sortAscending ? "▲" : "▼";

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
        if (node.NodeType == "Folder") return "📁";
        return GetFileIcon(node.MimeType);
    }

    protected static string GetFileIcon(string? mimeType)
    {
        if (mimeType is null) return "📄";
        if (mimeType.StartsWith("image/")) return "🖼️";
        if (mimeType.StartsWith("video/")) return "🎬";
        if (mimeType.StartsWith("audio/")) return "🎵";
        if (mimeType.StartsWith("text/")) return "📝";
        if (mimeType == "application/pdf") return "📕";
        if (mimeType.Contains("spreadsheet") || mimeType.Contains("excel")) return "📊";
        if (mimeType.Contains("presentation") || mimeType.Contains("powerpoint")) return "📈";
        if (mimeType.Contains("document") || mimeType.Contains("word")) return "📘";
        if (mimeType.Contains("zip") || mimeType.Contains("compressed")) return "🗜️";
        return "📄";
    }

    protected static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    // ── Shared item event handlers ─────────────────────────────────────────────

    /// <summary>Handles opening a shared item from the SharedWithMe or SharedByMe views.</summary>
    protected async Task HandleOpenSharedItem(SharedItemViewModel item)
    {
        await OnOpenSharedItem.InvokeAsync(item);
    }

    /// <summary>Handles declining a share from the SharedWithMe view.</summary>
    protected async Task HandleDeclineShare(SharedItemViewModel item)
    {
        await OnDeclineShare.InvokeAsync(item);
    }

    /// <summary>Handles managing a share from the SharedByMe view.</summary>
    protected async Task HandleManageShare(SharedItemViewModel item)
    {
        await OnManageShare.InvokeAsync(item);
    }

    /// <summary>Handles revoking a share from the SharedByMe view.</summary>
    protected async Task HandleRevokeShare(SharedItemViewModel item)
    {
        await OnRevokeShare.InvokeAsync(item);
    }

    /// <summary>Handles inline permission change from the SharedByMe view.</summary>
    protected async Task HandleSharePermissionChanged(SharePermissionChangedEventArgs args)
    {
        await OnSharePermissionChanged.InvokeAsync(args);
    }

    /// <summary>Handles copying a public link from the SharedByMe view.</summary>
    protected async Task HandleCopyShareLink(SharedItemViewModel item)
    {
        await OnCopyShareLink.InvokeAsync(item);
    }

    private async Task LoadCurrentFolderAsync()
    {
        var caller = await GetCallerContextAsync();

        var nodes = _currentFolderId.HasValue
            ? await FileService.ListChildrenAsync(_currentFolderId.Value, caller)
            : await FileService.ListRootAsync(caller);

        _nodes = nodes.Select(ToViewModel).ToList();
        StateHasChanged();
    }

    private async Task LoadCollaboraCapabilitiesAsync()
    {
        var options = CollaboraOptions.Value;
        _isCollaboraConfigured = options.Enabled &&
                                (!string.IsNullOrWhiteSpace(options.ServerUrl) || options.UseBuiltInCollabora);

        var preferredOrder = new[] { "docx", "xlsx", "pptx", "odt", "ods", "odp", "txt", "csv", "rtf" };

        if (!_isCollaboraConfigured)
        {
            _isCollaboraAvailable = false;
            _collaboraEditableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _supportedNewDocumentExtensions = [];
            return;
        }

        try
        {
            var discovery = await CollaboraDiscoveryService.DiscoverAsync();
            _isCollaboraAvailable = discovery.IsAvailable;

            if (_isCollaboraAvailable)
            {
                _collaboraEditableExtensions = discovery.Actions
                    .Where(a => string.Equals(a.Action, "edit", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(a.Action, "view", StringComparison.OrdinalIgnoreCase))
                    .Select(a => NormalizeExtension(a.Extension))
                    .Where(ext => !string.IsNullOrWhiteSpace(ext))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var ordered = preferredOrder
                    .Where(ext => _collaboraEditableExtensions.Contains(ext))
                    .Concat(_collaboraEditableExtensions.Where(ext => !preferredOrder.Contains(ext, StringComparer.OrdinalIgnoreCase)).OrderBy(ext => ext, StringComparer.OrdinalIgnoreCase));

                _supportedNewDocumentExtensions = [.. ordered];
            }
            else
            {
                _collaboraEditableExtensions = preferredOrder.ToHashSet(StringComparer.OrdinalIgnoreCase);
                _supportedNewDocumentExtensions = [.. preferredOrder];
            }
        }
        catch
        {
            _isCollaboraAvailable = false;
            _collaboraEditableExtensions = preferredOrder.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _supportedNewDocumentExtensions = [.. preferredOrder];
        }

        if (_supportedNewDocumentExtensions.Count > 0 && !_supportedNewDocumentExtensions.Contains(_selectedDocumentExtension, StringComparer.OrdinalIgnoreCase))
            _selectedDocumentExtension = _supportedNewDocumentExtensions[0];
    }

    private string EnsureUniqueFileName(string fileName)
    {
        var existingNames = _nodes.Select(n => n.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existingNames.Contains(fileName))
            return fileName;

        var extension = Path.GetExtension(fileName);
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var suffix = 1;
        var candidate = $"{baseName} ({suffix}){extension}";

        while (existingNames.Contains(candidate))
        {
            suffix++;
            candidate = $"{baseName} ({suffix}){extension}";
        }

        return candidate;
    }

    private static string BuildFileName(string name, string extension)
    {
        var normalizedName = name.Trim();
        var normalizedExtension = NormalizeExtension(extension);

        if (string.IsNullOrWhiteSpace(normalizedExtension))
            return normalizedName;

        return normalizedName.EndsWith($".{normalizedExtension}", StringComparison.OrdinalIgnoreCase)
            ? normalizedName
            : $"{normalizedName}.{normalizedExtension}";
    }

    private static string NormalizeExtension(string? extension) =>
        extension?.Trim().TrimStart('.').ToLowerInvariant() ?? string.Empty;

    private static string GetMimeType(string extension)
    {
        return NormalizeExtension(extension) switch
        {
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "odt" => "application/vnd.oasis.opendocument.text",
            "rtf" => "application/rtf",
            "txt" => "text/plain",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "ods" => "application/vnd.oasis.opendocument.spreadsheet",
            "csv" => "text/csv",
            "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "odp" => "application/vnd.oasis.opendocument.presentation",
            _ => "application/octet-stream"
        };
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

    private async Task DownloadNodeAsync(FileNodeViewModel node)
    {
        if (!string.Equals(node.NodeType, "File", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var effectiveUserId = UserId;
        if (effectiveUserId == Guid.Empty)
        {
            effectiveUserId = (await GetCallerContextAsync()).UserId;
        }

        var baseUrl = string.IsNullOrWhiteSpace(ApiBaseUrl)
            ? string.Empty
            : ApiBaseUrl.TrimEnd('/');

        var downloadUrl =
            $"{baseUrl}/api/v1/files/{node.Id}/download?userId={Uri.EscapeDataString(effectiveUserId.ToString())}";

        // forceLoad ensures a real browser navigation, which allows large file downloads.
        Navigation.NavigateTo(downloadUrl, forceLoad: true);
    }

    private static FileNodeViewModel ToViewModel(FileNodeDto dto)
    {
        return new FileNodeViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            NodeType = dto.NodeType,
            MimeType = dto.MimeType,
            Size = dto.Size,
            ParentId = dto.ParentId,
            IsFavorite = dto.IsFavorite,
            UpdatedAt = dto.UpdatedAt,
            Tags = dto.Tags.Select(t => new FileTagViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                FileCount = 0
            }).ToList()
        };
    }
}
