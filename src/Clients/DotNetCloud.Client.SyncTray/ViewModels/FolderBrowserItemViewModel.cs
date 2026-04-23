using System.Collections.ObjectModel;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// Represents a single folder node in the folder browser tree.
/// Supports three-state check: <c>true</c> = included, <c>false</c> = excluded,
/// <c>null</c> = mixed (some children included, some excluded).
/// </summary>
public sealed class FolderBrowserItemViewModel : ViewModelBase
{
    private bool? _isChecked = true;
    private bool _isExpanded;
    private bool _isSelectionLocked;
    private bool _suppressPropagation;

    /// <summary>Server-side node ID.</summary>
    public Guid NodeId { get; }

    /// <summary>Folder display name.</summary>
    public string Name { get; }

    /// <summary>Relative path from the sync root (e.g. "Documents/Projects").</summary>
    public string RelativePath { get; }

    /// <summary>Child folder nodes.</summary>
    public ObservableCollection<FolderBrowserItemViewModel> Children { get; } = [];

    /// <summary>Parent node reference for bubble-up propagation.</summary>
    public FolderBrowserItemViewModel? Parent { get; set; }

    /// <summary>Whether this tree node is expanded in the UI.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    /// <summary>Whether the user can change this node's selection state.</summary>
    public bool IsSelectionEnabled => !_isSelectionLocked;

    /// <summary>Whether this node is force-excluded from selective sync.</summary>
    public bool IsSelectionLocked
    {
        get => _isSelectionLocked;
        set
        {
            if (SetProperty(ref _isSelectionLocked, value))
            {
                OnPropertyChanged(nameof(IsSelectionEnabled));
            }
        }
    }

    /// <summary>
    /// Three-state check: <c>true</c> = included, <c>false</c> = excluded,
    /// <c>null</c> = mixed (indeterminate).
    /// </summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set
        {
            if (IsSelectionLocked && value != false)
            {
                value = false;
            }

            if (SetProperty(ref _isChecked, value) && !_suppressPropagation)
            {
                // Only propagate definite states down to children.
                if (value.HasValue)
                    PropagateCheckToChildren(value.Value);

                // Always bubble up to update parent state.
                Parent?.UpdateFromChildren();
            }
        }
    }

    /// <summary>Initializes a new <see cref="FolderBrowserItemViewModel"/>.</summary>
    public FolderBrowserItemViewModel(Guid nodeId, string name, string relativePath)
    {
        NodeId = nodeId;
        Name = name;
        RelativePath = relativePath;
    }

    private void PropagateCheckToChildren(bool isChecked)
    {
        foreach (var child in Children)
        {
            child._suppressPropagation = true;
            child.IsChecked = isChecked;
            child._suppressPropagation = false;
            child.PropagateCheckToChildren(isChecked);
        }
    }

    /// <summary>
    /// Re-evaluates this node's check state based on the states of its children.
    /// If all children are checked → <c>true</c>, all unchecked → <c>false</c>,
    /// mixed → <c>null</c> (indeterminate).
    /// </summary>
    internal void UpdateFromChildren()
    {
        if (Children.Count == 0) return;

        var allChecked = Children.All(c => c.IsChecked == true);
        var allUnchecked = Children.All(c => c.IsChecked == false);

        _suppressPropagation = true;
        if (allChecked)
            IsChecked = true;
        else if (allUnchecked)
            IsChecked = false;
        else
            IsChecked = null;
        _suppressPropagation = false;

        Parent?.UpdateFromChildren();
    }
}
