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

    /// <summary>Loads the folder tree from the server.</summary>
    public ICommand LoadTreeCommand { get; }

    /// <summary>Saves the current folder selection as selective sync rules.</summary>
    public ICommand SaveCommand { get; }

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
            var tree = await _ipc.GetFolderTreeAsync(_contextId);
            if (tree is null)
            {
                ErrorMessage = "Failed to load folder tree from server.";
                return;
            }

            // Build view-model tree from the API response (folders only).
            foreach (var child in tree.Children)
            {
                if (!string.Equals(child.NodeType, "Folder", StringComparison.OrdinalIgnoreCase))
                    continue;

                var item = BuildItem(child, parentPath: string.Empty, parent: null);
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
        }
    }

    /// <summary>Persists the current folder selection as selective sync rules.</summary>
    public async Task SaveAsync()
    {
        _selectiveSync.ClearRules(_contextId);

        CollectExcludedPaths(RootItems, path =>
            _selectiveSync.Exclude(_contextId, path));

        await _selectiveSync.SaveAsync(_configFilePath);
    }

    // TODO: Add lazy loading of children for large trees.
    private static FolderBrowserItemViewModel BuildItem(
        SyncTreeNodeResponse node, string parentPath, FolderBrowserItemViewModel? parent)
    {
        var relativePath = string.IsNullOrEmpty(parentPath)
            ? node.Name
            : $"{parentPath}/{node.Name}";

        var item = new FolderBrowserItemViewModel(node.NodeId, node.Name, relativePath)
        {
            Parent = parent,
        };

        foreach (var child in node.Children)
        {
            if (!string.Equals(child.NodeType, "Folder", StringComparison.OrdinalIgnoreCase))
                continue;

            var childItem = BuildItem(child, relativePath, item);
            item.Children.Add(childItem);
        }

        return item;
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
}
