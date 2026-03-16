using System.Collections.ObjectModel;
using System.Windows.Input;
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.SyncTray.Ipc;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// View-model for the selective-sync folder browser.
/// Fetches the server-side folder tree via IPC and lets the user
/// check/uncheck folders to include or exclude from sync.
/// </summary>
public sealed class FolderBrowserViewModel : ViewModelBase
{
    private readonly IIpcClient _ipc;
    private readonly Guid _contextId;
    private readonly ISelectiveSyncConfig _selectiveSync;
    private readonly string _configFilePath;
    private SyncTreeNodeResponse? _fullTree;

    private bool _isLoading;
    private string? _errorMessage;

    /// <summary>Root-level folder nodes.</summary>
    public ObservableCollection<FolderBrowserItemViewModel> RootItems { get; } = [];

    /// <summary>Whether the tree is currently being loaded.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>Error message from the last load attempt, or <c>null</c>.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>
    /// True when loading completed successfully but the server has no subdirectories.
    /// In this case all files will be synced and selective-sync is not applicable.
    /// </summary>
    public bool NoFoldersFound => !IsLoading && ErrorMessage is null && RootItems.Count == 0;

    /// <summary>Loads the folder tree from the server.</summary>
    public ICommand LoadTreeCommand { get; }

    /// <summary>Saves the current folder selection as selective sync rules.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>
    /// Optional sync root path for local file cleanup when folders are excluded.
    /// Set when the view-model is used within a context that has a local folder.
    /// </summary>
    public string? LocalSyncRoot { get; set; }

    /// <summary>
    /// Callback invoked before deleting local files for a newly excluded folder.
    /// Returns <c>true</c> to confirm deletion, <c>false</c> to skip.
    /// Defaults to always-true (no confirmation) for non-UI/test scenarios.
    /// </summary>
    public Func<string, Task<bool>>? ConfirmDeletionAsync { get; set; }

    /// <summary>Initializes a new <see cref="FolderBrowserViewModel"/>.</summary>
    /// <param name="ipc">IPC client for communicating with SyncService.</param>
    /// <param name="contextId">Sync context ID to load the tree for.</param>
    /// <param name="selectiveSync">Selective sync config to persist rules to.</param>
    /// <param name="configFilePath">Path to the selective sync config JSON file.</param>
    public FolderBrowserViewModel(
        IIpcClient ipc,
        Guid contextId,
        ISelectiveSyncConfig selectiveSync,
        string configFilePath)
    {
        _ipc = ipc;
        _contextId = contextId;
        _selectiveSync = selectiveSync;
        _configFilePath = configFilePath;

        LoadTreeCommand = new AsyncRelayCommand(LoadTreeAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    /// <summary>Fetches the folder tree from the server and populates <see cref="RootItems"/>.</summary>
    public async Task LoadTreeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        RootItems.Clear();

        try
        {
            _fullTree = await _ipc.GetFolderTreeAsync(_contextId);
            if (_fullTree is null)
            {
                ErrorMessage = "Failed to load folder tree from server.";
                return;
            }

            // Build top-level items only (children are lazy-loaded on expand).
            foreach (var child in _fullTree.Children)
            {
                if (!IsFolderNodeType(child.NodeType))
                    continue;

                var item = BuildItemLazy(child, parentPath: string.Empty, parent: null);
                RootItems.Add(item);
            }

            // Apply existing selective sync rules to pre-set check states.
            ApplyExistingRules();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading folder tree: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(NoFoldersFound));
        }
    }

    /// <summary>Persists the current folder selection as selective sync rules.</summary>
    public async Task SaveAsync()
    {
        ErrorMessage = null;
        // Collect previously excluded paths before saving new state.
        var previousExclusions = new HashSet<string>(
            _selectiveSync.GetRules(_contextId)
                .Where(r => !r.IsInclude)
                .Select(r => r.FolderPath),
            StringComparer.OrdinalIgnoreCase);

        _selectiveSync.ClearRules(_contextId);

        var newExclusions = new List<string>();
        CollectExcludedPaths(RootItems, path =>
        {
            _selectiveSync.Exclude(_contextId, path);
            newExclusions.Add(path);
        });

        await _ipc.UpdateSelectiveSyncAsync(_contextId, _selectiveSync.GetRules(_contextId), CancellationToken.None);

        // Issue #58: clean up local files for newly excluded folders.
        if (LocalSyncRoot is not null)
        {
            foreach (var excluded in newExclusions)
            {
                if (previousExclusions.Contains(excluded))
                    continue; // Already excluded before — no cleanup needed.

                await CleanupExcludedFolderAsync(excluded);
            }
        }
    }

    /// <summary>Surfaces a save-time error in the existing error panel.</summary>
    public void SetSaveError(string message) => ErrorMessage = message;

    /// <summary>
    /// Builds a tree item with lazy-loaded children. Child nodes are populated
    /// as placeholder stubs; actual children are loaded when the node is expanded.
    /// </summary>
    private FolderBrowserItemViewModel BuildItemLazy(
        SyncTreeNodeResponse node, string parentPath, FolderBrowserItemViewModel? parent)
    {
        var relativePath = string.IsNullOrEmpty(parentPath)
            ? node.Name
            : $"{parentPath}/{node.Name}";

        var item = new FolderBrowserItemViewModel(node.NodeId, node.Name, relativePath)
        {
            Parent = parent,
        };

        // Check if this node has folder children at all.
        var hasChildFolders = node.Children.Any(c => IsFolderNodeType(c.NodeType));

        if (hasChildFolders)
        {
            // Add a loading placeholder so the UI shows an expander arrow.
            item.Children.Add(new FolderBrowserItemViewModel(
                Guid.Empty, "(loading...)", $"{relativePath}/(loading)"));
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FolderBrowserItemViewModel.IsExpanded) && item.IsExpanded)
                    _ = LoadChildrenAsync(item, node);
            };
        }

        return item;
    }

    /// <summary>
    /// Lazy-loads children for a node on first expand. Replaces the placeholder.
    /// </summary>
    internal async Task LoadChildrenAsync(FolderBrowserItemViewModel item, SyncTreeNodeResponse sourceNode)
    {
        // Guard against double-load.
        if (item.Children.Count == 1 && item.Children[0].NodeId == Guid.Empty)
        {
            item.Children.Clear();

            foreach (var child in sourceNode.Children)
            {
                if (!IsFolderNodeType(child.NodeType))
                    continue;

                var childItem = BuildItemLazy(child, item.RelativePath, item);
                item.Children.Add(childItem);
            }

            // Re-apply rules for the newly loaded children.
            var rules = _selectiveSync.GetRules(_contextId);
            if (rules.Count > 0)
            {
                var excludedPaths = new HashSet<string>(
                    rules.Where(r => !r.IsInclude).Select(r => r.FolderPath),
                    StringComparer.OrdinalIgnoreCase);
                ApplyRulesToItems(item.Children, excludedPaths);
            }
        }

        await Task.CompletedTask;
    }

    private void ApplyExistingRules()
    {
        var rules = _selectiveSync.GetRules(_contextId);
        if (rules.Count == 0) return;

        var excludedPaths = new HashSet<string>(
            rules.Where(r => !r.IsInclude).Select(r => r.FolderPath),
            StringComparer.OrdinalIgnoreCase);

        ApplyRulesToItems(RootItems, excludedPaths);
    }

    private static void ApplyRulesToItems(
        IEnumerable<FolderBrowserItemViewModel> items,
        HashSet<string> excludedPaths)
    {
        foreach (var item in items)
        {
            if (excludedPaths.Contains(item.RelativePath))
                item.IsChecked = false;

            ApplyRulesToItems(item.Children, excludedPaths);
        }
    }

    private static void CollectExcludedPaths(
        IEnumerable<FolderBrowserItemViewModel> items,
        Action<string> excludeAction)
    {
        foreach (var item in items)
        {
            if (item.IsChecked == false)
            {
                // Entire subtree is excluded — record the parent only.
                excludeAction(item.RelativePath);
            }
            else if (item.IsChecked == null)
            {
                // Mixed state — recurse into children.
                CollectExcludedPaths(item.Children, excludeAction);
            }
            // IsChecked == true → fully included, nothing to record.
        }
    }

    private static bool IsFolderNodeType(string? nodeType) =>
        string.Equals(nodeType, "Folder", StringComparison.OrdinalIgnoreCase)
        || string.Equals(nodeType, "Directory", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Deletes local files for a folder that was newly excluded from sync.
    /// Shows a confirmation dialog via <see cref="ConfirmDeletionAsync"/> before proceeding.
    /// </summary>
    private async Task CleanupExcludedFolderAsync(string relativePath)
    {
        if (LocalSyncRoot is null) return;

        var localPath = Path.Combine(LocalSyncRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(localPath)) return;

        // Ask for confirmation before deleting.
        if (ConfirmDeletionAsync is not null)
        {
            var confirmed = await ConfirmDeletionAsync(relativePath);
            if (!confirmed) return;
        }

        try
        {
            Directory.Delete(localPath, recursive: true);
        }
        catch
        {
            // Silently skip — files may be in use or permissions may prevent deletion.
        }
    }
}
