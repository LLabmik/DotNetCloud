using System.Security.Claims;
using System.Net.Http.Json;
using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.DTOs;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
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
    [Inject] private ITrashService TrashService { get; set; } = default!;
    [Inject] private IQuotaService QuotaService { get; set; } = default!;
    [Inject] private IVersionService VersionService { get; set; } = default!;
    [Inject] private IShareService ShareService { get; set; } = default!;
    [Inject] private ITagService TagService { get; set; } = default!;
    [Inject] private ICommentService CommentService { get; set; } = default!;
    [Inject] private IUserManagementService UserManagementService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<FileBrowser> Logger { get; set; } = default!;

    /// <summary>The current user ID, used for opening the document editor.</summary>
    [Parameter] public Guid UserId { get; set; }

    /// <summary>Base URL for the Files API (e.g., "https://cloud.example.com"), used for the document editor.</summary>
    [Parameter] public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Optional file ID to navigate to on load (deep-link from search results).</summary>
    [Parameter] public string? FileId { get; set; }

    /// <summary>Navigation nonce — changes each time a search result is clicked, even for the same file.</summary>
    [Parameter] public string? FileIdNav { get; set; }

    private FileSidebarSection _activeSection = FileSidebarSection.AllFiles;
    private int _trashItemCount;
    private long _trashBytes;

    private List<SharedItemViewModel> _sharedWithMeItems = [];
    private List<SharedItemViewModel> _sharedByMeItems = [];
    private bool _isLoadingSharedItems;

    /// <summary>Items shared with the current user (for the SharedWithMe view).</summary>
    public IReadOnlyList<SharedItemViewModel> SharedWithMeItems => _sharedWithMeItems;

    /// <summary>Items shared by the current user (for the SharedByMe view).</summary>
    public IReadOnlyList<SharedItemViewModel> SharedByMeItems => _sharedByMeItems;

    /// <summary>Whether shared items are being loaded.</summary>
    public bool IsLoadingSharedItems => _isLoadingSharedItems;

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
    private bool _showSingleTagDialog;
    private Guid? _singleTagNodeId;
    private List<Guid> _tagTargetNodeIds = [];
    private string _newFolderName = string.Empty;
    private string _newDocumentName = "Untitled";
    private string _selectedDocumentExtension = "docx";
    private string _customExtension = string.Empty;
    private bool _useCustomExtension;
    private string _freeformFileName = string.Empty;
    private bool _isCollaboraAvailable;
    private bool _isCollaboraConfigured;
    private HashSet<string> _collaboraEditableExtensions = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _supportedNewFileExtensions = [];
    private List<string> _collaboraNewFileExtensions = [];
    private ElementReference _freeformInput;

    /// <summary>Extensions that should always open in native editors, never in Collabora.</summary>
    private static readonly HashSet<string> NativeEditorExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "md", "markdown", "json", "xml", "yaml", "yml", "csv", "log",
        "html", "htm", "css", "js", "ts", "cs", "py", "sh", "bash",
        "sql", "ini", "toml", "cfg", "conf", "env"
    };

    /// <summary>Base file extensions always available in the New File dialog, regardless of Collabora.</summary>
    private static readonly string[] BaseFileExtensions =
    [
        "txt", "md", "json", "html", "xml", "csv", "yaml", "css", "js", "py", "sh"
    ];
    private FileNodeViewModel? _shareTargetNode;
    private FileNodeViewModel? _previewNode;
    private FileNodeViewModel? _editorNode;
    private int _currentPage = 1;
    // Selection mode
    private bool _selectionMode;

    // Folder picker dialog
    private bool _showFolderPicker;
    private FolderPickerMode _folderPickerMode;
    private Guid? _pickerCurrentFolderId;
    private readonly List<BreadcrumbItem> _pickerBreadcrumbs = [];
    private List<FileNodeViewModel> _pickerFolders = [];    private int _pageSize = 50;
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
    private bool _hasDroppedFiles;
    private DotNetObjectReference<FileBrowser>? _dropBridgeRef;
    private DotNetObjectReference<FileBrowser>? _pasteRef;
    private DotNetObjectReference<FileBrowser>? _contextMenuRef;
    private DotNetObjectReference<FileBrowser>? _dragMoveRef;

    // Context menu state
    private bool _showContextMenu;
    private double _contextMenuX;
    private double _contextMenuY;
    private Guid _contextMenuNodeId;
    private string _contextMenuNodeType = "File";
    private bool _contextMenuNodeIsReadOnly;

    // Inline rename dialog
    private bool _showRenameDialog;
    private FileNodeViewModel? _renameNode;
    private string _renameNewName = string.Empty;

    // Version history panel
    private bool _showVersionHistory;
    private Guid _versionHistoryNodeId;
    private string? _versionHistoryFileName;
    private List<FileVersionViewModel> _versionHistoryItems = [];

    // Comments panel
    private bool _showComments;
    private Guid _commentsNodeId;
    private string? _commentsFileName;
    private List<FileCommentViewModel> _commentItems = [];
    private Guid _currentUserId;
    private string? _lastHandledNav;
    private bool _currentFolderIsReadOnly;

    protected override async Task OnInitializedAsync()
    {
        var caller = await GetCallerContextAsync();
        _currentUserId = caller.UserId;
        await LoadCollaboraCapabilitiesAsync();
        await LoadCurrentFolderAsync();
        await LoadTrashCountAsync();
        await LoadQuotaAsync();
        await LoadUserTagsAsync();

        // Deep-link: navigate to a specific file (e.g., from search results)
        if (Guid.TryParse(FileId, out var fileId))
        {
            _lastHandledNav = FileIdNav;
            await NavigateToFileAsync(fileId);
        }
    }

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        // Handle FileId changes when already on the page (same-page navigation).
        // FileIdNav is a timestamp nonce that changes on every click, even for the same file.
        if (!string.IsNullOrEmpty(FileId) && FileIdNav != _lastHandledNav && Guid.TryParse(FileId, out var fileId))
        {
            _lastHandledNav = FileIdNav;
            await NavigateToFileAsync(fileId);
        }
    }

    /// <summary>Handles sidebar section navigation.</summary>
    protected async Task HandleSectionChanged(FileSidebarSection section)
    {
        _activeSection = section;

        if (section == FileSidebarSection.AllFiles)
        {
            _currentFolderId = null;
            _breadcrumbs.Clear();
            await LoadCurrentFolderAsync();
        }
        else if (section == FileSidebarSection.Favorites)
        {
            _currentFolderId = null;
            _breadcrumbs.Clear();
            await LoadFavoritesAsync();
        }
        else if (section == FileSidebarSection.Recent)
        {
            _currentFolderId = null;
            _breadcrumbs.Clear();
            await LoadRecentAsync();
        }
        else if (section == FileSidebarSection.SharedWithMe)
        {
            await LoadSharedWithMeAsync();
        }
        else if (section == FileSidebarSection.SharedByMe)
        {
            await LoadSharedByMeAsync();
        }

        StateHasChanged();
    }

    /// <summary>Handles tag selection from the sidebar.</summary>
    protected async Task HandleTagSelected(FileTagViewModel tag)
    {
        _activeSection = FileSidebarSection.Tags;
        _activeTag = tag;
        _taggedNodes = [];
        StateHasChanged();

        try
        {
            var caller = await GetCallerContextAsync();
            var nodes = await TagService.GetNodesByTagAsync(tag.Name, caller);
            _taggedNodes = nodes.Select(n => new FileNodeViewModel
            {
                Id = n.Id,
                Name = n.Name,
                NodeType = n.NodeType,
                MimeType = n.MimeType,
                Size = n.Size,
                ParentId = n.ParentId,
                UpdatedAt = n.UpdatedAt,
                Tags = n.Tags.Select(t => new FileTagViewModel { Id = t.Id, Name = t.Name, Color = t.Color }).ToList()
            }).ToList();
        }
        catch
        {
            _taggedNodes = [];
        }

        StateHasChanged();
    }

    /// <summary>Called when the trash bin contents change (item restored, purged, or emptied).</summary>
    protected async Task HandleTrashChanged()
    {
        await LoadTrashCountAsync();
        StateHasChanged();
    }

    private async Task LoadTrashCountAsync()
    {
        try
        {
            var caller = await GetCallerContextAsync();
            var items = await TrashService.ListTrashAsync(caller);
            _trashItemCount = items.Count;
            _trashBytes = items.Sum(i => i.Size);
        }
        catch
        {
            _trashItemCount = 0;
            _trashBytes = 0;
        }
    }

    private async Task LoadQuotaAsync()
    {
        try
        {
            var caller = await GetCallerContextAsync();
            var dto = await QuotaService.GetOrCreateQuotaAsync(caller.UserId, caller);
            _quota = new QuotaViewModel
            {
                UsedBytes = dto.UsedBytes,
                MaxBytes = dto.MaxBytes,
                UsagePercent = dto.UsagePercent
            };
        }
        catch
        {
            _quota = null;
        }
    }

    private async Task LoadUserTagsAsync()
    {
        try
        {
            var caller = await GetCallerContextAsync();
            var summaries = await TagService.GetUserTagSummariesAsync(caller);
            _userTags = summaries.Select(s => new FileTagViewModel
            {
                Name = s.Name,
                Color = s.Color,
                FileCount = s.FileCount
            }).ToList();
        }
        catch
        {
            _userTags = [];
        }
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
                ("Size", true)  => _nodes.OrderBy(n => n.NodeType != "Folder").ThenBy(n => n.NodeType == "Folder" ? n.TotalSize : n.Size),
                ("Size", false) => _nodes.OrderBy(n => n.NodeType != "Folder").ThenByDescending(n => n.NodeType == "Folder" ? n.TotalSize : n.Size),
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
    protected bool IsCurrentFolderReadOnly => _currentFolderIsReadOnly;
    protected bool CanCreateNewFile => _supportedNewFileExtensions.Count > 0;
    protected IReadOnlyList<string> SupportedNewFileExtensions => _supportedNewFileExtensions;
    protected string CustomExtension { get => _customExtension; set => _customExtension = value; }
    protected bool UseCustomExtension { get => _useCustomExtension; set => _useCustomExtension = value; }
    protected string FreeformFileName { get => _freeformFileName; set => _freeformFileName = value; }
    protected IReadOnlyList<string> CollaboraNewFileExtensions => _collaboraNewFileExtensions;
    protected bool IsShowBulkTagAdd => _showBulkTagAdd;
    protected FileTagViewModel? ActiveTag => _activeTag;
    protected IReadOnlyList<FileNodeViewModel> TaggedNodes => _taggedNodes;
    protected IReadOnlyList<FileTagViewModel> UserTags => _userTags;
    protected bool IsAllSelected => _nodes.Count > 0 && _selectedNodes.Count == _nodes.Count;
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The bridge is idempotent and can be called multiple times safely.
        _dropBridgeRef ??= DotNetObjectReference.Create(this);
        await Js.InvokeVoidAsync("dotnetcloudFilesDrop.init", ".files-browser", _dropBridgeRef);

        // Initialize paste image handler for the file browser
        if (firstRender)
        {
            _pasteRef ??= DotNetObjectReference.Create(this);
            await Js.InvokeVoidAsync("dotnetcloudFilePaste.init", ".files-browser", _pasteRef);

            _contextMenuRef ??= DotNetObjectReference.Create(this);
            await Js.InvokeVoidAsync("dotnetcloudContextMenu.init", ".files-browser", _contextMenuRef);

            _dragMoveRef ??= DotNetObjectReference.Create(this);
            await Js.InvokeVoidAsync("dotnetcloudDragMove.init", ".files-browser", _dragMoveRef);
        }
        else
        {
            // Refresh draggable attributes after content changes (navigation, etc.)
            await Js.InvokeVoidAsync("dotnetcloudDragMove.refresh");
        }
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
        else
        {
            // Trim breadcrumbs to the clicked folder (handles breadcrumb-back navigation)
            var idx = _breadcrumbs.FindIndex(b => b.Id == folderId);
            if (idx >= 0)
            {
                _breadcrumbs.RemoveRange(idx + 1, _breadcrumbs.Count - idx - 1);
            }
        }

        _ = LoadCurrentFolderAsync();
    }

    /// <summary>
    /// Navigates to a specific file by ID: loads its parent folder, builds breadcrumbs,
    /// then opens the file in preview. Used for deep-linking from search results.
    /// </summary>
    private async Task NavigateToFileAsync(Guid fileId)
    {
        try
        {
            var caller = await GetCallerContextAsync();
            var node = await FileService.GetNodeAsync(fileId, caller);
            if (node is null)
            {
                Logger.LogWarning("Deep-link file {FileId} not found", fileId);
                return;
            }

            // Navigate to the file's parent folder
            if (node.ParentId.HasValue)
            {
                // Build breadcrumb trail by walking up the folder tree
                var ancestors = new List<(Guid Id, string Name)>();
                var currentId = node.ParentId;
                while (currentId.HasValue)
                {
                    var folder = await FileService.GetNodeAsync(currentId.Value, caller);
                    if (folder is null) break;
                    ancestors.Add((folder.Id, folder.Name));
                    currentId = folder.ParentId;
                }

                _breadcrumbs.Clear();
                ancestors.Reverse();
                foreach (var ancestor in ancestors)
                {
                    _breadcrumbs.Add(new BreadcrumbItem(ancestor.Id, ancestor.Name));
                }

                _currentFolderId = node.ParentId;
                _currentPage = 1;
                _selectedNodes.Clear();
                await LoadCurrentFolderAsync();
            }

            // Open the file (preview or editor)
            var vm = ToViewModel(node);
            await HandleNodeDoubleClick(vm);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to navigate to file {FileId}", fileId);
        }
    }

    protected void HandleNodeClick(FileNodeViewModel node)
    {
        if (_selectionMode)
        {
            ToggleSelect(node.Id);
            return;
        }

        if (_selectedNodes.Contains(node.Id))
            _selectedNodes.Remove(node.Id);
        else
            _selectedNodes.Add(node.Id);
    }

    /// <summary>Toggles selection of a single item.</summary>
    protected void ToggleSelect(Guid id)
    {
        if (!_selectedNodes.Add(id)) _selectedNodes.Remove(id);
    }

    /// <summary>Selects or deselects all items.</summary>
    protected void ToggleSelectAll()
    {
        if (IsAllSelected)
            _selectedNodes.Clear();
        else
            foreach (var node in _nodes) _selectedNodes.Add(node.Id);
    }

    /// <summary>Toggles selection mode on/off. Clears selection when exiting.</summary>
    protected void ToggleSelectionMode()
    {
        _selectionMode = !_selectionMode;
        if (!_selectionMode)
            _selectedNodes.Clear();
    }

    protected async Task HandleNodeDoubleClick(FileNodeViewModel node)
    {
        Console.WriteLine($"[DIAG-OPEN] HandleNodeDoubleClick called: Name={node.Name} Type={node.NodeType} Mime={node.MimeType}");
        Console.WriteLine($"[DIAG-OPEN] CanNative={CanOpenInNativePreview(node)} CanDocEditor={CanOpenInDocumentEditor(node)}");

        if (node.NodeType == "Folder")
        {
            _breadcrumbs.Add(new BreadcrumbItem(node.Id, node.Name));
            NavigateToFolder(node.Id);
        }
        else if (CanOpenInNativePreview(node))
        {
            Console.WriteLine("[DIAG-OPEN] → ShowPreview (native)");
            ShowPreview(node);
        }
        else if (CanOpenInDocumentEditor(node))
        {
            Console.WriteLine("[DIAG-OPEN] → ShowDocumentEditor");
            ShowDocumentEditor(node);
        }
        else
        {
            Console.WriteLine("[DIAG-OPEN] → ShowPreview (fallback)");
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
        _hasDroppedFiles = false;
    }

    protected void ShowCreateDocumentDialog()
    {
        _showCreateDocument = true;
        _newDocumentName = "Untitled";
        _freeformFileName = string.Empty;
        _customExtension = string.Empty;
        _useCustomExtension = false;

        if (_collaboraNewFileExtensions.Count > 0 &&
            !_collaboraNewFileExtensions.Contains(_selectedDocumentExtension, StringComparer.OrdinalIgnoreCase))
            _selectedDocumentExtension = _collaboraNewFileExtensions[0];
    }

    protected void HideCreateDocumentDialog() => _showCreateDocument = false;

    protected async Task HandleCreateDocumentKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await CreateDocumentAsync();

        if (e.Key == "Escape")
            _showCreateDocument = false;
    }

    protected async Task HandleFreeformFileKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await CreateFreeformFileAsync();

        if (e.Key == "Escape")
            _showCreateDocument = false;
    }

    protected async Task CreateDocumentAsync()
    {
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

        var viewModel = ToViewModel(created);
        if (CanOpenInDocumentEditor(created.Name))
        {
            ShowDocumentEditor(viewModel);
        }
        else if (CanOpenInNativePreview(viewModel))
        {
            ShowPreview(viewModel);
        }
    }

    protected async Task CreateFreeformFileAsync()
    {
        var raw = string.IsNullOrWhiteSpace(_freeformFileName) ? "Untitled.txt" : _freeformFileName.Trim();
        var extension = NormalizeExtension(Path.GetExtension(raw));
        var fileName = EnsureUniqueFileName(raw);

        var caller = await GetCallerContextAsync();
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

        var viewModel = ToViewModel(created);
        if (CanOpenInDocumentEditor(created.Name))
        {
            ShowDocumentEditor(viewModel);
        }
        else if (CanOpenInNativePreview(viewModel))
        {
            ShowPreview(viewModel);
        }
    }

    protected async Task HandleUploadComplete()
    {
        _showUploadDialog = false;
        _hasDroppedFiles = false;
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

    /// <summary>Receives dropped files via JS drop bridge and opens the upload dialog pre-populated.</summary>
    [JSInvokable]
    public void OnFilesDropped(object[] _)
    {
        _dragEnterCount = 0;
        _hasDroppedFiles = true;
        _showUploadDialog = true;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>Called from JS when an image is pasted from the clipboard. Opens the upload dialog.</summary>
    [JSInvokable]
    public void OnImagePasted(string fileName, long fileSize)
    {
        _hasDroppedFiles = true;
        _showUploadDialog = true;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>Called from JS when a pasted image exceeds the max upload size.</summary>
    [JSInvokable]
    public void OnPasteError(string errorMessage)
    {
        // Error is surfaced when the upload dialog opens; for now log it
        InvokeAsync(StateHasChanged);
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    /// <summary>Called from JS when the user right-clicks a file item.</summary>
    [JSInvokable]
    public void OnContextMenu(string nodeId, string nodeType, double x, double y)
    {
        if (Guid.TryParse(nodeId, out var id))
        {
            var node = _nodes.FirstOrDefault(candidate => candidate.Id == id);
            _contextMenuNodeId = id;
            _contextMenuNodeType = node?.NodeType ?? nodeType;
            _contextMenuNodeIsReadOnly = node?.IsReadOnly == true;
            _contextMenuX = x;
            _contextMenuY = y;
            _showContextMenu = true;
            InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>Called from JS to dismiss the context menu.</summary>
    [JSInvokable]
    public void OnContextMenuDismiss()
    {
        _showContextMenu = false;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>Dismisses the context menu (Blazor-side).</summary>
    protected void DismissContextMenu()
    {
        _showContextMenu = false;
        _contextMenuNodeIsReadOnly = false;
    }

    /// <summary>Context menu: Open file or folder.</summary>
    protected async Task HandleContextOpen(Guid nodeId)
    {
        _showContextMenu = false;
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        if (node.NodeType == "Folder")
            OpenFolder(node);
        else
            await OpenNodeAsync(node);
    }

    /// <summary>Context menu: Show inline rename dialog.</summary>
    protected void HandleContextRename(Guid nodeId)
    {
        _showContextMenu = false;
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        _renameNode = node;
        _renameNewName = node.Name;
        _showRenameDialog = true;
    }

    /// <summary>Context menu: Show move folder picker for a single item.</summary>
    protected async Task HandleContextMove(Guid nodeId)
    {
        _showContextMenu = false;
        _selectedNodes.Clear();
        _selectedNodes.Add(nodeId);
        _folderPickerMode = FolderPickerMode.Move;
        await OpenFolderPicker();
    }

    /// <summary>Context menu: Show copy folder picker for a single item.</summary>
    protected async Task HandleContextCopy(Guid nodeId)
    {
        _showContextMenu = false;
        _selectedNodes.Clear();
        _selectedNodes.Add(nodeId);
        _folderPickerMode = FolderPickerMode.Copy;
        await OpenFolderPicker();
    }

    /// <summary>Context menu: Open share dialog for a node.</summary>
    protected async Task HandleContextShare(Guid nodeId)
    {
        _showContextMenu = false;
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is not null)
            await ShowShareDialogAsync(node);
    }

    /// <summary>Context menu: Download a file.</summary>
    protected async Task HandleContextDownload(Guid nodeId)
    {
        _showContextMenu = false;
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is not null)
            await DownloadNodeAsync(node);
    }

    /// <summary>Context menu: Delete (trash) a node.</summary>
    protected async Task HandleContextDelete(Guid nodeId)
    {
        _showContextMenu = false;
        var caller = await GetCallerContextAsync();
        await FileService.DeleteAsync(nodeId, caller);
        await LoadCurrentFolderAsync();
        await LoadTrashCountAsync();
    }

    /// <summary>Context menu: Open version history panel for a file.</summary>
    protected async Task HandleContextVersionHistory(Guid nodeId)
    {
        _showContextMenu = false;
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null || node.NodeType == "Folder") return;

        _versionHistoryNodeId = nodeId;
        _versionHistoryFileName = node.Name;
        _versionHistoryItems = [];
        _showVersionHistory = true;
        StateHasChanged();

        var caller = await GetCallerContextAsync();
        var versions = await VersionService.ListVersionsAsync(nodeId, caller);
        var maxVersion = versions.Count > 0 ? versions.Max(v => v.VersionNumber) : 0;

        // Resolve display names for all unique version authors
        var versionUserIds = versions.Select(v => v.CreatedByUserId).Distinct().ToList();
        var versionNameMap = new Dictionary<Guid, string>();
        foreach (var uid in versionUserIds)
        {
            var user = await UserManagementService.GetUserAsync(uid);
            versionNameMap[uid] = user?.DisplayName ?? uid.ToString()[..8];
        }

        _versionHistoryItems = versions.Select(v => new FileVersionViewModel
        {
            Id = v.Id,
            VersionNumber = v.VersionNumber,
            Label = v.Label,
            CreatedAt = v.CreatedAt,
            AuthorName = versionNameMap[v.CreatedByUserId],
            SizeBytes = v.Size,
            IsCurrent = v.VersionNumber == maxVersion
        }).ToList();
        StateHasChanged();
    }

    /// <summary>Hides the version history panel.</summary>
    protected void HideVersionHistory()
    {
        _showVersionHistory = false;
        _versionHistoryFileName = null;
        _versionHistoryItems = [];
    }

    /// <summary>Handles downloading a specific version from the version history panel.</summary>
    protected async Task HandleDownloadVersion(FileVersionViewModel version)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == _versionHistoryNodeId);
        if (node is null) return;
        var url = $"/api/v1/files/{_versionHistoryNodeId}/download?version={version.VersionNumber}";
        await Js.InvokeVoidAsync("open", url, "_blank");
    }

    /// <summary>Handles restoring a file to a specific version.</summary>
    protected async Task HandleRestoreVersion(FileVersionViewModel version)
    {
        var caller = await GetCallerContextAsync();
        await VersionService.RestoreVersionAsync(_versionHistoryNodeId, version.Id, caller);
        await HandleContextVersionHistory(_versionHistoryNodeId);
        await LoadCurrentFolderAsync();
    }

    /// <summary>Handles deleting a specific version.</summary>
    protected async Task HandleDeleteVersion(FileVersionViewModel version)
    {
        var caller = await GetCallerContextAsync();
        await VersionService.DeleteVersionAsync(version.Id, caller);
        _versionHistoryItems.RemoveAll(v => v.Id == version.Id);
        StateHasChanged();
    }

    /// <summary>Handles saving a version label.</summary>
    protected async Task HandleVersionLabelSaved((Guid VersionId, string Label) args)
    {
        var caller = await GetCallerContextAsync();
        await VersionService.LabelVersionAsync(args.VersionId, args.Label, caller);
    }

    // ── Comments panel ───────────────────────────────────────────────────────

    /// <summary>Context menu: Open comments panel for a file.</summary>
    protected async Task HandleContextComments(Guid nodeId)
    {
        _showContextMenu = false;
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null) return;

        _commentsNodeId = nodeId;
        _commentsFileName = node.Name;
        _commentItems = [];
        _showComments = true;
        StateHasChanged();

        await LoadCommentsAsync();
    }

    /// <summary>Hides the comments panel.</summary>
    protected void HideComments()
    {
        _showComments = false;
        _commentsFileName = null;
        _commentItems = [];
    }

    /// <summary>Loads comments for the current node.</summary>
    private async Task LoadCommentsAsync()
    {
        var caller = await GetCallerContextAsync();
        var comments = await CommentService.GetCommentsAsync(_commentsNodeId, caller);

        // Resolve display names for all unique comment authors
        var userIds = comments.Select(c => c.CreatedByUserId).Distinct().ToList();
        var nameMap = new Dictionary<Guid, string>();
        foreach (var uid in userIds)
        {
            var user = await UserManagementService.GetUserAsync(uid);
            nameMap[uid] = user?.DisplayName ?? uid.ToString()[..8];
        }

        _commentItems = comments.Select(c => new FileCommentViewModel
        {
            Id = c.Id,
            FileNodeId = c.FileNodeId,
            ParentCommentId = c.ParentCommentId,
            Content = c.Content,
            CreatedByUserId = c.CreatedByUserId,
            AuthorName = nameMap[c.CreatedByUserId],
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            ReplyCount = c.ReplyCount
        }).ToList();
        StateHasChanged();
    }

    /// <summary>Handles adding a new top-level comment.</summary>
    protected async Task HandleAddComment(string content)
    {
        var caller = await GetCallerContextAsync();
        await CommentService.AddCommentAsync(_commentsNodeId, content, null, caller);
        await LoadCommentsAsync();
        UpdateNodeCommentCount(_commentsNodeId, _commentItems.Count);
    }

    /// <summary>Handles adding a reply to an existing comment.</summary>
    protected async Task HandleReplyComment((Guid ParentCommentId, string Content) args)
    {
        var caller = await GetCallerContextAsync();
        await CommentService.AddCommentAsync(_commentsNodeId, args.Content, args.ParentCommentId, caller);
        await LoadCommentsAsync();
        UpdateNodeCommentCount(_commentsNodeId, _commentItems.Count);
    }

    /// <summary>Handles editing an existing comment.</summary>
    protected async Task HandleEditComment((Guid CommentId, string Content) args)
    {
        var caller = await GetCallerContextAsync();
        await CommentService.EditCommentAsync(args.CommentId, args.Content, caller);
        await LoadCommentsAsync();
    }

    /// <summary>Handles deleting a comment.</summary>
    protected async Task HandleDeleteComment(Guid commentId)
    {
        var caller = await GetCallerContextAsync();
        await CommentService.DeleteCommentAsync(commentId, caller);
        _commentItems.RemoveAll(c => c.Id == commentId);
        UpdateNodeCommentCount(_commentsNodeId, _commentItems.Count);
        StateHasChanged();
    }

    // ── Inline rename ────────────────────────────────────────────────────────

    /// <summary>Confirms the inline rename operation.</summary>
    protected async Task ConfirmRename()
    {
        if (_renameNode is null || string.IsNullOrWhiteSpace(_renameNewName)) return;

        var caller = await GetCallerContextAsync();
        await FileService.RenameAsync(_renameNode.Id, new RenameNodeDto { Name = _renameNewName.Trim() }, caller);

        _showRenameDialog = false;
        _renameNode = null;
        _renameNewName = string.Empty;
        await LoadCurrentFolderAsync();
    }

    /// <summary>Cancels the rename operation.</summary>
    protected void CancelRename()
    {
        _showRenameDialog = false;
        _renameNode = null;
        _renameNewName = string.Empty;
    }

    /// <summary>Handles keyboard events in the rename dialog.</summary>
    protected async Task HandleRenameKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await ConfirmRename();
        if (e.Key == "Escape") CancelRename();
    }

    // ── Drag-and-drop move ──────────────────────────────────────────────────

    /// <summary>
    /// JS callback: user dragged a node onto a folder to move it.
    /// </summary>
    [JSInvokable]
    public async Task OnDragMoveNode(string sourceNodeId, string targetFolderId)
    {
        if (!Guid.TryParse(sourceNodeId, out var sourceId) ||
            !Guid.TryParse(targetFolderId, out var targetId))
            return;

        // Don't move a folder into itself
        if (sourceId == targetId) return;

        try
        {
            var caller = await GetCallerContextAsync();
            await FileService.MoveAsync(sourceId, new MoveNodeDto { TargetParentId = targetId }, caller);
            await LoadCurrentFolderAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception)
        {
            // Move failed — reload to show current state
            await LoadCurrentFolderAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    /// <summary>Legacy handler kept for the hidden InputFile (fallback). Opens upload dialog.</summary>
    protected void HandleBrowserFileDrop(InputFileChangeEventArgs e)
    {
        _dragEnterCount = 0;
        _hasDroppedFiles = true;
        _showUploadDialog = true;
    }

    public async ValueTask DisposeAsync()
    {
        _dropBridgeRef?.Dispose();
        _pasteRef?.Dispose();
        _contextMenuRef?.Dispose();
        _dragMoveRef?.Dispose();

        try
        {
            await Js.InvokeVoidAsync("dotnetcloudFilePaste.dispose");
            await Js.InvokeVoidAsync("dotnetcloudContextMenu.dispose");
            await Js.InvokeVoidAsync("dotnetcloudDragMove.dispose");
        }
        catch (JSDisconnectedException)
        {
            // Circuit may already be disconnected during disposal
        }
        catch (InvalidOperationException)
        {
            // JSInterop not available during prerender
        }
    }

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

    // ── Bulk actions ─────────────────────────────────────────────────────────

    /// <summary>Moves all selected items to trash.</summary>
    protected async Task BulkTrashSelected()
    {
        await DeleteSelected();
        await LoadTrashCountAsync();
    }

    /// <summary>Downloads all selected items as a ZIP archive.</summary>
    protected async Task BulkDownloadZip()
    {
        if (_selectedNodes.Count == 0) return;

        var nodeIds = _selectedNodes.ToList();
        var idsParam = string.Join(",", nodeIds);
        var effectiveUserId = UserId;
        if (effectiveUserId == Guid.Empty)
            effectiveUserId = (await GetCallerContextAsync()).UserId;

        var baseUrl = string.IsNullOrWhiteSpace(ApiBaseUrl) ? string.Empty : ApiBaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/api/v1/files/download-zip?userId={Uri.EscapeDataString(effectiveUserId.ToString())}";

        // Use JS to POST the node IDs and trigger a browser download
        await Js.InvokeVoidAsync("dotnetcloudFiles.downloadZip", url, nodeIds);
    }

    /// <summary>Shows the folder picker for moving selected items.</summary>
    protected async Task ShowMoveDialog()
    {
        _folderPickerMode = FolderPickerMode.Move;
        await OpenFolderPicker();
    }

    /// <summary>Shows the folder picker for copying selected items.</summary>
    protected async Task ShowCopyDialog()
    {
        _folderPickerMode = FolderPickerMode.Copy;
        await OpenFolderPicker();
    }

    /// <summary>Hides the folder picker dialog.</summary>
    protected void HideFolderPicker() => _showFolderPicker = false;

    /// <summary>Navigates the folder picker to a given folder.</summary>
    protected async Task PickerNavigate(Guid? folderId)
    {
        _pickerCurrentFolderId = folderId;

        if (folderId is null)
        {
            _pickerBreadcrumbs.Clear();
        }
        else
        {
            // If navigating to a folder already in breadcrumbs, truncate
            var existingIdx = _pickerBreadcrumbs.FindIndex(b => b.Id == folderId.Value);
            if (existingIdx >= 0)
            {
                _pickerBreadcrumbs.RemoveRange(existingIdx + 1, _pickerBreadcrumbs.Count - existingIdx - 1);
            }
            else
            {
                var folder = _pickerFolders.FirstOrDefault(f => f.Id == folderId.Value);
                if (folder is not null)
                    _pickerBreadcrumbs.Add(new BreadcrumbItem(folder.Id, folder.Name));
            }
        }

        await LoadPickerFoldersAsync();
    }

    /// <summary>Confirms the folder picker selection and executes the bulk operation.</summary>
    protected async Task ConfirmFolderPicker()
    {
        if (_selectedNodes.Count == 0)
        {
            HideFolderPicker();
            return;
        }

        var caller = await GetCallerContextAsync();
        var targetId = _pickerCurrentFolderId;

        if (_folderPickerMode == FolderPickerMode.Move)
        {
            foreach (var nodeId in _selectedNodes.ToList())
            {
                await FileService.MoveAsync(nodeId, new MoveNodeDto { TargetParentId = targetId }, caller);
            }
        }
        else
        {
            foreach (var nodeId in _selectedNodes.ToList())
            {
                await FileService.CopyAsync(nodeId, targetId, caller);
            }
        }

        _selectedNodes.Clear();
        _selectionMode = false;
        HideFolderPicker();

        // Navigate to the target folder so the user sees the result
        _currentFolderId = targetId;
        _breadcrumbs.Clear();

        await LoadCurrentFolderAsync();
    }

    private async Task OpenFolderPicker()
    {
        _pickerCurrentFolderId = null;
        _pickerBreadcrumbs.Clear();
        await LoadPickerFoldersAsync();
        _showFolderPicker = true;
    }

    private async Task LoadPickerFoldersAsync()
    {
        var caller = await GetCallerContextAsync();
        var nodes = _pickerCurrentFolderId.HasValue
            ? await FileService.ListChildrenAsync(_pickerCurrentFolderId.Value, caller)
            : await FileService.ListRootAsync(caller);

        _pickerFolders = nodes
            .Where(n => n.NodeType == "Folder")
            .Select(ToViewModel)
            .ToList();
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
        var caller = await GetCallerContextAsync();
        await TagService.BulkAddTagAsync(SelectedNodeIds, tag.Name, tag.Color, caller);
        await OnBulkTagAdd.InvokeAsync((SelectedNodeIds, tag.Name, tag.Color));
        await LoadCurrentFolderAsync();
        await LoadUserTagsAsync();
    }

    /// <summary>Returns the IDs of all currently selected nodes.</summary>
    protected IReadOnlyList<Guid> SelectedNodeIds => [.. _selectedNodes];

    // ── Single-file tag dialog ───────────────────────────────────────────────

    /// <summary>Context menu: Open tag dialog. If multiple nodes are selected, tags all of them.</summary>
    protected void HandleContextTag(Guid nodeId)
    {
        _showContextMenu = false;

        // If the right-clicked node is part of the current selection, target all selected nodes.
        // Otherwise, target just the right-clicked node.
        if (_selectedNodes.Count > 1 && _selectedNodes.Contains(nodeId))
        {
            _tagTargetNodeIds = [.. _selectedNodes];
        }
        else
        {
            _tagTargetNodeIds = [nodeId];
        }

        _singleTagNodeId = nodeId;
        _showSingleTagDialog = true;
    }

    /// <summary>Hides the single-file tag dialog.</summary>
    protected void HideSingleTagDialog()
    {
        _showSingleTagDialog = false;
        _singleTagNodeId = null;
        _tagTargetNodeIds = [];
    }

    /// <summary>Adds a tag to the target node(s).</summary>
    protected async Task HandleSingleTagAdd((string Name, string? Color) tag)
    {
        if (_tagTargetNodeIds.Count == 0) return;

        var caller = await GetCallerContextAsync();

        if (_tagTargetNodeIds.Count == 1)
        {
            var dto = await TagService.AddTagAsync(_tagTargetNodeIds[0], tag.Name, tag.Color, caller);
            var node = _nodes.FirstOrDefault(n => n.Id == _tagTargetNodeIds[0]);
            if (node is not null)
            {
                node.Tags = [.. node.Tags, new FileTagViewModel { Id = dto.Id, Name = dto.Name, Color = dto.Color }];
            }
        }
        else
        {
            await TagService.BulkAddTagAsync(_tagTargetNodeIds, tag.Name, tag.Color, caller);
            await LoadCurrentFolderAsync();
        }

        await LoadUserTagsAsync();
        StateHasChanged();
    }

    /// <summary>Removes a tag from the primary target node.</summary>
    protected async Task HandleSingleTagRemove(FileTagViewModel tag)
    {
        if (_singleTagNodeId is null) return;

        var caller = await GetCallerContextAsync();
        await TagService.RemoveTagAsync(_singleTagNodeId.Value, tag.Id, caller);

        // Update the local node's tag list
        var node = _nodes.FirstOrDefault(n => n.Id == _singleTagNodeId);
        if (node is not null)
        {
            node.Tags = [.. node.Tags.Where(t => t.Id != tag.Id)];
        }

        await LoadUserTagsAsync();
        StateHasChanged();
    }

    protected async Task ToggleFavorite(Guid nodeId)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is not null)
        {
            node.IsFavorite = !node.IsFavorite;
            StateHasChanged();

            try
            {
                var caller = await GetCallerContextAsync();
                await FileService.ToggleFavoriteAsync(nodeId, caller);
            }
            catch
            {
                // Revert on failure
                node.IsFavorite = !node.IsFavorite;
                StateHasChanged();
            }
        }
    }

    /// <summary>Removes a tag directly from the inline badge in the file list.</summary>
    protected async Task HandleInlineTagRemove(Guid nodeId, FileTagViewModel tag)
    {
        var caller = await GetCallerContextAsync();
        await TagService.RemoveTagAsync(nodeId, tag.Id, caller);

        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is not null)
        {
            node.Tags = [.. node.Tags.Where(t => t.Id != tag.Id)];
        }

        await LoadUserTagsAsync();
        StateHasChanged();
    }

    protected async Task ShowShareDialogAsync(FileNodeViewModel node)
    {
        _shareTargetNode = node;
        _shareDialogShares = [];
        _isLoadingShareDialogShares = true;
        _showShareDialog = true;
        StateHasChanged();

        try
        {
            var caller = await GetCallerContextAsync();
            var shares = await ShareService.GetSharesAsync(node.Id, caller);
            _shareDialogShares = shares.Select(s => new ShareViewModel
            {
                Id = s.Id,
                ShareType = s.ShareType,
                RecipientName = s.SharedWithUserId?.ToString() ?? "Public Link",
                Permission = s.Permission,
                LinkToken = s.LinkToken,
                LinkUrl = s.LinkToken is not null ? $"{Navigation.BaseUri.TrimEnd('/')}/s/{s.LinkToken}" : null,
                HasPassword = s.HasPassword,
                DownloadCount = s.DownloadCount,
                MaxDownloads = s.MaxDownloads,
                ExpiresAt = s.ExpiresAt,
                CreatedAt = s.CreatedAt,
                Note = s.Note
            }).ToList();
        }
        catch
        {
            _shareDialogShares = [];
        }
        finally
        {
            _isLoadingShareDialogShares = false;
        }
    }

    protected void HideShareDialog() => _showShareDialog = false;

    private List<ShareViewModel> _shareDialogShares = [];
    private bool _isLoadingShareDialogShares;

    protected async Task<IReadOnlyList<ShareSearchResult>> HandleShareSearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        var result = await UserManagementService.ListUsersAsync(new UserListQuery
        {
            Search = query,
            PageSize = 10,
            IsActive = true
        });

        return result.Items.Select(u => new ShareSearchResult
        {
            Id = u.Id,
            DisplayName = u.DisplayName,
            SecondaryText = null,
            ResultType = "User"
        }).ToList();
    }

    protected async Task HandleShareCreatedAsync(ShareCreatedEventArgs args)
    {
        if (_shareTargetNode is null) return;

        var caller = await GetCallerContextAsync();
        var dto = new CreateShareDto
        {
            ShareType = args.ShareType,
            SharedWithUserId = args.ShareType == "User" ? args.TargetId : null,
            SharedWithTeamId = args.ShareType == "Team" ? args.TargetId : null,
            SharedWithGroupId = args.ShareType == "Group" ? args.TargetId : null,
            Permission = args.Permission,
            ExpiresAt = args.ExpirationDays > 0 ? DateTime.UtcNow.AddDays(args.ExpirationDays) : null,
            Note = args.Note
        };

        await ShareService.CreateShareAsync(_shareTargetNode.Id, dto, caller);
    }

    protected async Task HandleShareUpdatedAsync(ShareUpdatedEventArgs args)
    {
        var caller = await GetCallerContextAsync();
        var dto = new UpdateShareDto
        {
            Permission = args.NewPermission,
            MaxDownloads = args.NewMaxDownloads,
            ExpiresAt = args.NewExpirationDays > 0 ? DateTime.UtcNow.AddDays((double)args.NewExpirationDays) : null,
            LinkPassword = args.RemovePassword ? "" : args.NewPassword
        };

        var result = await ShareService.UpdateShareAsync(args.ShareId, dto, caller);

        // Update local share state so UI reflects changes (e.g. HasPassword)
        var idx = _shareDialogShares.FindIndex(s => s.Id == args.ShareId);
        if (idx >= 0)
        {
            var old = _shareDialogShares[idx];
            _shareDialogShares[idx] = new ShareViewModel
            {
                Id = result.Id,
                ShareType = result.ShareType,
                RecipientName = old.RecipientName,
                Permission = result.Permission,
                LinkToken = result.LinkToken,
                LinkUrl = result.LinkToken is not null ? $"{Navigation.BaseUri.TrimEnd('/')}/s/{result.LinkToken}" : old.LinkUrl,
                HasPassword = result.HasPassword,
                DownloadCount = result.DownloadCount,
                MaxDownloads = result.MaxDownloads,
                ExpiresAt = result.ExpiresAt,
                CreatedAt = result.CreatedAt,
                Note = result.Note
            };
            StateHasChanged();
        }
    }

    protected async Task HandleShareRemovedAsync(Guid shareId)
    {
        var caller = await GetCallerContextAsync();
        await ShareService.DeleteShareAsync(shareId, caller);
        _shareDialogShares.RemoveAll(s => s.Id == shareId);
    }

    protected async Task HandlePublicLinkToggledAsync(bool enabled)
    {
        if (_shareTargetNode is null) return;

        var caller = await GetCallerContextAsync();

        if (enabled)
        {
            var dto = new CreateShareDto
            {
                ShareType = "PublicLink",
                Permission = "Read"
            };

            var result = await ShareService.CreateShareAsync(_shareTargetNode.Id, dto, caller);

            // Update the dialog shares list with the real server-created share
            var linkUrl = result.LinkToken is not null
                ? $"{Navigation.BaseUri.TrimEnd('/')}/s/{result.LinkToken}"
                : null;

            // Replace placeholder with real share
            var placeholder = _shareDialogShares.FirstOrDefault(s => s.ShareType == "PublicLink");
            if (placeholder is not null)
            {
                _shareDialogShares.Remove(placeholder);
            }

            _shareDialogShares.Add(new ShareViewModel
            {
                Id = result.Id,
                ShareType = result.ShareType,
                RecipientName = "Public Link",
                Permission = result.Permission,
                LinkToken = result.LinkToken,
                LinkUrl = linkUrl,
                HasPassword = result.HasPassword,
                DownloadCount = result.DownloadCount,
                MaxDownloads = result.MaxDownloads,
                ExpiresAt = result.ExpiresAt,
                CreatedAt = result.CreatedAt
            });

            StateHasChanged();
        }
    }

    protected void ShowPreview(FileNodeViewModel node)
    {
        _previewNode = node;
        _showPreview = true;
    }

    protected void HidePreview() => _showPreview = false;

    /// <summary>Closes the preview and opens the share dialog for the given node.</summary>
    protected async Task HandlePreviewShare(FileNodeViewModel node)
    {
        HidePreview();
        await ShowShareDialogAsync(node);
    }

    /// <summary>Closes the preview and triggers a download for the given node.</summary>
    protected async Task HandlePreviewDownload(FileNodeViewModel node)
    {
        HidePreview();
        await DownloadNodeAsync(node);
    }

    /// <summary>Opens the comments panel from file preview.</summary>
    protected async Task HandlePreviewComments(FileNodeViewModel node)
    {
        await HandleContextComments(node.Id);
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
        node.NodeType == "File" && !node.IsReadOnly && CanOpenInDocumentEditor(node.Name);

    private bool CanOpenInDocumentEditor(string fileName)
    {
        if (!_isCollaboraConfigured || !_isCollaboraAvailable)
            return false;

        var extension = NormalizeExtension(Path.GetExtension(fileName));
        return !string.IsNullOrWhiteSpace(extension) && _collaboraEditableExtensions.Contains(extension);
    }

    /// <summary>Returns true when the file can be rendered natively (image, text, video, audio, PDF) without Collabora.</summary>
    private static bool CanOpenInNativePreview(FileNodeViewModel node)
    {
        if (node.NodeType != "File") return false;

        var mime = node.MimeType;
        if (!string.IsNullOrEmpty(mime))
        {
            if (mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return true;
            if (mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return true;
            if (mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return true;
            if (mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase)) return true;
            if (mime.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)) return true;
            if (mime.Equals("application/json", StringComparison.OrdinalIgnoreCase)) return true;
            if (mime.Equals("application/xml", StringComparison.OrdinalIgnoreCase)) return true;
        }

        // Fallback: check extension for common natively previewable types
        var ext = NormalizeExtension(Path.GetExtension(node.Name));
        return ext is "png" or "jpg" or "jpeg" or "gif" or "webp" or "svg" or "bmp" or "ico"
            or "mp4" or "webm" or "ogg" or "ogv"
            or "mp3" or "wav" or "flac" or "aac" or "oga"
            or "pdf"
            or "txt" or "md" or "csv" or "log" or "json" or "xml" or "yaml" or "yml"
            or "html" or "htm" or "css" or "js" or "ts" or "cs" or "py" or "sh" or "bash"
            or "sql" or "ini" or "toml" or "cfg" or "conf" or "env" or "rtf";
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

    /// <summary>Handles opening a shared item — fetches the node and shows preview or editor.</summary>
    protected async Task HandleOpenSharedItem(SharedItemViewModel item)
    {
        Logger.LogInformation("[SharedOpen] Opening shared item NodeId={NodeId} Name={Name} Type={Type}",
            item.NodeId, item.NodeName, item.NodeType);
        try
        {
            var caller = await GetCallerContextAsync();
            Logger.LogInformation("[SharedOpen] Caller UserId={UserId}", caller.UserId);

            var nodeDto = await FileService.GetNodeAsync(item.NodeId, caller);
            if (nodeDto is null)
            {
                Logger.LogWarning("[SharedOpen] GetNodeAsync returned null for NodeId={NodeId}", item.NodeId);
                return;
            }

            Logger.LogInformation("[SharedOpen] Got node: {Name} Type={Type}", nodeDto.Name, nodeDto.NodeType);
            var node = ToViewModel(nodeDto);

            if (node.NodeType == "Folder")
            {
                _activeSection = FileSidebarSection.AllFiles;
                _breadcrumbs.Clear();
                _breadcrumbs.Add(new BreadcrumbItem(node.Id, node.Name));
                NavigateToFolder(node.Id);
            }
            else if (CanOpenInNativePreview(node))
            {
                Logger.LogInformation("[SharedOpen] Showing native preview");
                ShowPreview(node);
            }
            else if (CanOpenInDocumentEditor(node))
            {
                Logger.LogInformation("[SharedOpen] Opening in document editor");
                ShowDocumentEditor(node);
            }
            else
            {
                Logger.LogInformation("[SharedOpen] Showing preview");
                ShowPreview(node);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[SharedOpen] Exception opening shared item NodeId={NodeId}", item.NodeId);
        }
    }

    /// <summary>Handles declining (deleting) a share from the SharedWithMe view.</summary>
    protected async Task HandleDeclineShare(SharedItemViewModel item)
    {
        var caller = await GetCallerContextAsync();
        await ShareService.DeleteShareAsync(item.ShareId, caller);
        _sharedWithMeItems.RemoveAll(s => s.ShareId == item.ShareId);
        StateHasChanged();
    }

    /// <summary>Handles managing a share from the SharedByMe view — opens share dialog.</summary>
    protected Task HandleManageShare(SharedItemViewModel item)
    {
        // Could open share dialog for the node
        return Task.CompletedTask;
    }

    /// <summary>Handles revoking a share from the SharedByMe view.</summary>
    protected async Task HandleRevokeShare(SharedItemViewModel item)
    {
        var caller = await GetCallerContextAsync();
        await ShareService.DeleteShareAsync(item.ShareId, caller);
        _sharedByMeItems.RemoveAll(s => s.ShareId == item.ShareId);
        StateHasChanged();
    }

    /// <summary>Handles inline permission change from the SharedByMe view.</summary>
    protected async Task HandleSharePermissionChanged(SharePermissionChangedEventArgs args)
    {
        var caller = await GetCallerContextAsync();
        await ShareService.UpdateShareAsync(args.ShareId, new UpdateShareDto { Permission = args.NewPermission }, caller);
    }

    /// <summary>Handles copying a public link from the SharedByMe view.</summary>
    protected async Task HandleCopyShareLink(SharedItemViewModel item)
    {
        if (!string.IsNullOrEmpty(item.LinkUrl))
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", item.LinkUrl);
        }
    }

    private async Task LoadCurrentFolderAsync()
    {
        var caller = await GetCallerContextAsync();

        _currentFolderIsReadOnly = _currentFolderId.HasValue
            && (await FileService.GetNodeAsync(_currentFolderId.Value, caller))?.IsReadOnly == true;

        var nodes = _currentFolderId.HasValue
            ? await FileService.ListChildrenAsync(_currentFolderId.Value, caller)
            : await FileService.ListRootAsync(caller);

        _nodes = nodes.Select(ToViewModel).ToList();

        // Fetch comment counts for all visible nodes in a single query
        var nodeIds = _nodes.Select(n => n.Id).ToList();
        if (nodeIds.Count > 0)
        {
            var counts = await CommentService.GetCommentCountsAsync(nodeIds, caller);
            foreach (var node in _nodes)
            {
                if (counts.TryGetValue(node.Id, out var count))
                    node.CommentCount = count;
            }
        }

        StateHasChanged();
    }

    private async Task LoadFavoritesAsync()
    {
        var caller = await GetCallerContextAsync();
        var nodes = await FileService.ListFavoritesAsync(caller);
        _nodes = nodes.Select(ToViewModel).ToList();
        StateHasChanged();
    }

    private async Task LoadRecentAsync()
    {
        var caller = await GetCallerContextAsync();
        var nodes = await FileService.ListRecentAsync(50, caller);
        _nodes = nodes.Select(ToViewModel).ToList();
        StateHasChanged();
    }

    private async Task LoadSharedWithMeAsync()
    {
        _isLoadingSharedItems = true;
        StateHasChanged();

        try
        {
            var caller = await GetCallerContextAsync();
            var shares = await ShareService.GetSharedWithMeAsync(caller);

            var userIds = shares.Select(s => s.CreatedByUserId).Distinct().ToList();
            var nameMap = await ResolveUserNamesAsync(userIds);

            _sharedWithMeItems = shares.Select(s => new SharedItemViewModel
            {
                ShareId = s.Id,
                NodeId = s.FileNodeId,
                NodeName = s.NodeName ?? "Unknown",
                ShareType = s.ShareType,
                Permission = s.Permission,
                SharedByName = nameMap.GetValueOrDefault(s.CreatedByUserId, s.CreatedByUserId.ToString()),
                SharedAt = s.CreatedAt,
                ExpiresAt = s.ExpiresAt,
                DownloadCount = s.DownloadCount,
                MaxDownloads = s.MaxDownloads
            }).ToList();
        }
        catch
        {
            _sharedWithMeItems = [];
        }
        finally
        {
            _isLoadingSharedItems = false;
        }
    }

    private async Task LoadSharedByMeAsync()
    {
        _isLoadingSharedItems = true;
        StateHasChanged();

        try
        {
            var caller = await GetCallerContextAsync();
            var shares = await ShareService.GetSharedByMeAsync(caller);

            var userIds = shares
                .Where(s => s.SharedWithUserId.HasValue)
                .Select(s => s.SharedWithUserId!.Value)
                .Distinct()
                .ToList();
            var nameMap = await ResolveUserNamesAsync(userIds);

            _sharedByMeItems = shares.Select(s => new SharedItemViewModel
            {
                ShareId = s.Id,
                NodeId = s.FileNodeId,
                NodeName = s.NodeName ?? "Unknown",
                ShareType = s.ShareType,
                Permission = s.Permission,
                SharedWithName = s.SharedWithUserId.HasValue
                    ? nameMap.GetValueOrDefault(s.SharedWithUserId.Value, s.SharedWithUserId.Value.ToString())
                    : "Public Link",
                SharedAt = s.CreatedAt,
                ExpiresAt = s.ExpiresAt,
                LinkUrl = s.LinkToken is not null ? $"{Navigation.BaseUri.TrimEnd('/')}/s/{s.LinkToken}" : null,
                DownloadCount = s.DownloadCount,
                MaxDownloads = s.MaxDownloads
            }).ToList();
        }
        catch
        {
            _sharedByMeItems = [];
        }
        finally
        {
            _isLoadingSharedItems = false;
        }
    }

    private async Task<Dictionary<Guid, string>> ResolveUserNamesAsync(IReadOnlyList<Guid> userIds)
    {
        var map = new Dictionary<Guid, string>();
        foreach (var id in userIds)
        {
            var user = await UserManagementService.GetUserAsync(id);
            if (user is not null)
            {
                map[id] = user.DisplayName;
            }
        }
        return map;
    }

    private async Task LoadCollaboraCapabilitiesAsync()
    {
        var options = CollaboraOptions.Value;
        _isCollaboraConfigured = options.Enabled &&
                                (!string.IsNullOrWhiteSpace(options.ServerUrl) || options.UseBuiltInCollabora);

        var preferredOrder = new[] { "docx", "xlsx", "pptx", "odt", "ods", "odp" };

        if (_isCollaboraConfigured)
        {
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

                    // Remove extensions that have native editors — native always wins
                    _collaboraEditableExtensions.ExceptWith(NativeEditorExtensions);
                }
                else
                {
                    _collaboraEditableExtensions = preferredOrder.ToHashSet(StringComparer.OrdinalIgnoreCase);
                }
            }
            catch
            {
                _isCollaboraAvailable = false;
                _collaboraEditableExtensions = preferredOrder.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }
        else
        {
            _isCollaboraAvailable = false;
            _collaboraEditableExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        // Build the New File extension list: Collabora-supported (preferred order) + base text extensions
        var ordered = preferredOrder
            .Where(ext => _collaboraEditableExtensions.Contains(ext))
            .Concat(_collaboraEditableExtensions
                .Where(ext => !preferredOrder.Contains(ext, StringComparer.OrdinalIgnoreCase))
                .OrderBy(ext => ext, StringComparer.OrdinalIgnoreCase))
            .Concat(BaseFileExtensions
                .Where(ext => !_collaboraEditableExtensions.Contains(ext)));

        _supportedNewFileExtensions = [.. ordered.Distinct(StringComparer.OrdinalIgnoreCase)];

        // Collabora-only extensions for the document row (excludes base text types)
        _collaboraNewFileExtensions = [.. _supportedNewFileExtensions
            .Where(ext => _collaboraEditableExtensions.Contains(ext))];

        if (_collaboraNewFileExtensions.Count > 0 && !_collaboraNewFileExtensions.Contains(_selectedDocumentExtension, StringComparer.OrdinalIgnoreCase))
            _selectedDocumentExtension = _collaboraNewFileExtensions[0];
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
            "docm" => "application/vnd.ms-word.document.macroEnabled.12",
            "odt" => "application/vnd.oasis.opendocument.text",
            "rtf" => "application/rtf",
            "txt" or "log" or "ini" or "cfg" or "conf" or "env" => "text/plain",
            "md" or "markdown" => "text/markdown",
            "json" => "application/json",
            "xml" => "application/xml",
            "html" or "htm" => "text/html",
            "css" => "text/css",
            "js" => "text/javascript",
            "ts" => "text/typescript",
            "cs" => "text/x-csharp",
            "py" => "text/x-python",
            "sh" or "bash" => "text/x-shellscript",
            "sql" => "text/x-sql",
            "yaml" or "yml" => "text/yaml",
            "toml" => "text/toml",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "xlsm" => "application/vnd.ms-excel.sheet.macroEnabled.12",
            "ods" => "application/vnd.oasis.opendocument.spreadsheet",
            "csv" => "text/csv",
            "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "odp" => "application/vnd.oasis.opendocument.presentation",
            _ => "text/plain"
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
            TotalSize = dto.TotalSize,
            ParentId = dto.ParentId,
            IsFavorite = dto.IsFavorite,
            UpdatedAt = dto.UpdatedAt,
            CurrentVersion = dto.CurrentVersion,
            Tags = dto.Tags.Select(t => new FileTagViewModel
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                FileCount = 0
            }).ToList(),
            IsVirtual = dto.IsVirtual,
            IsReadOnly = dto.IsReadOnly,
            VirtualSourceKind = dto.VirtualSourceKind,
            VirtualSourceId = dto.VirtualSourceId,
            VirtualRelativePath = dto.VirtualRelativePath,
        };
    }

    private void UpdateNodeCommentCount(Guid nodeId, int count)
    {
        var node = _nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is not null)
            node.CommentCount = count;
    }
}
