using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.SyncTray.ViewModels;

namespace DotNetCloud.Client.SyncTray.Views;

/// <summary>
/// Dialog for adding an additional local sync folder to an existing account.
/// Creates a same-named remote folder by default, or maps to an existing remote folder.
/// </summary>
public partial class AddFolderDialog : Window
{
    private readonly AddFolderDialogViewModel _vm;

    /// <summary>Initializes the dialog with no context (required by the Avalonia runtime loader).</summary>
    public AddFolderDialog() : this(null!, Guid.Empty, []) { }

    /// <summary>Initializes the dialog for a specific account context.</summary>
    public AddFolderDialog(ISyncContextManager syncManager, Guid contextId, IReadOnlyList<string> existingLocalRoots)
    {
        InitializeComponent();
        _vm = new AddFolderDialogViewModel(this, syncManager, contextId, existingLocalRoots);
        DataContext = _vm;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddFolderDialogViewModel.DialogResult))
        {
            Close(_vm.DialogResult);
        }
    }
}

/// <summary>
/// View-model for the Add Folder dialog. Handles the local folder picker, the remote
/// folder name (pre-populated from the local folder's leaf name), an optional parent
/// folder, an optional existing-folder picker, overlap validation, and remote folder creation.
/// </summary>
public sealed class AddFolderDialogViewModel : ViewModelBase
{
    private readonly Window _owner;
    private readonly ISyncContextManager _syncManager;
    private readonly Guid _contextId;
    private readonly IReadOnlyList<string> _existingLocalRoots;

    private string _localFolderPath = string.Empty;
    private bool _useExistingRemoteFolder;
    private string _remoteFolderName = string.Empty;
    private string _remoteParentPath = string.Empty;
    private Guid? _remoteParentNodeId;
    private string _existingRemoteFolderPath = string.Empty;
    private Guid? _existingRemoteNodeId;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private AddFolderResult? _dialogResult;

    /// <summary>Chosen local folder path.</summary>
    public string LocalFolderPath
    {
        get => _localFolderPath;
        set => SetProperty(ref _localFolderPath, value);
    }

    /// <summary>True to map to an existing remote folder; false to create a new one.</summary>
    public bool UseExistingRemoteFolder
    {
        get => _useExistingRemoteFolder;
        set => SetProperty(ref _useExistingRemoteFolder, value);
    }

    /// <summary>Name for the new remote folder (pre-populated from the local folder name).</summary>
    public string RemoteFolderName
    {
        get => _remoteFolderName;
        set => SetProperty(ref _remoteFolderName, value);
    }

    /// <summary>Display path of the chosen parent folder, or empty for top level.</summary>
    public string RemoteParentPath
    {
        get => _remoteParentPath;
        set => SetProperty(ref _remoteParentPath, value);
    }

    /// <summary>Display path of the chosen existing remote folder.</summary>
    public string ExistingRemoteFolderPath
    {
        get => _existingRemoteFolderPath;
        set => SetProperty(ref _existingRemoteFolderPath, value);
    }

    /// <summary>Validation error message.</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Whether an async operation is in progress.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Non-null when the dialog should close with a result.</summary>
    public AddFolderResult? DialogResult
    {
        get => _dialogResult;
        private set => SetProperty(ref _dialogResult, value);
    }

    /// <summary>Opens the local folder picker.</summary>
    public ICommand BrowseFolderCommand { get; }

    /// <summary>Opens the remote folder picker for the existing-folder mode.</summary>
    public ICommand ChooseRemoteFolderCommand { get; }

    /// <summary>Opens the remote folder picker to choose a parent for the new folder.</summary>
    public ICommand ChooseParentFolderCommand { get; }

    /// <summary>Validates input, creates/selects the remote folder, and returns the result.</summary>
    public ICommand ConfirmCommand { get; }

    /// <summary>Cancels the dialog.</summary>
    public ICommand CancelCommand { get; }

    /// <summary>Initializes the view-model.</summary>
    public AddFolderDialogViewModel(
        Window owner,
        ISyncContextManager syncManager,
        Guid contextId,
        IReadOnlyList<string> existingLocalRoots)
    {
        _owner = owner;
        _syncManager = syncManager;
        _contextId = contextId;
        _existingLocalRoots = existingLocalRoots;

        BrowseFolderCommand = new RelayCommand(async () => await BrowseFolderAsync());
        ChooseRemoteFolderCommand = new RelayCommand(async () => await ChooseRemoteFolderAsync());
        ChooseParentFolderCommand = new RelayCommand(async () => await ChooseParentFolderAsync());
        ConfirmCommand = new AsyncRelayCommand(ConfirmAsync);
        CancelCommand = new RelayCommand(() => _owner.Close(null));
    }

    /// <summary>Derives a remote folder name from a local folder path (the leaf name).</summary>
    internal static string DeriveRemoteFolderName(string localFolderPath)
    {
        var leaf = Path.GetFileName(Path.TrimEndingDirectorySeparator(localFolderPath));
        return leaf ?? string.Empty;
    }

    /// <summary>Records the chosen parent folder (null = top level). Used by the picker and tests.</summary>
    internal void SetParentFolder(Guid? nodeId, string? relativePath)
    {
        _remoteParentNodeId = nodeId;
        RemoteParentPath = string.IsNullOrWhiteSpace(relativePath)
            ? string.Empty
            : "/" + relativePath.Trim('/');
    }

    /// <summary>Records the chosen existing remote folder. Used by the picker and tests.</summary>
    internal void SetExistingRemoteFolder(Guid nodeId, string? relativePath)
    {
        _existingRemoteNodeId = nodeId;
        ExistingRemoteFolderPath = "/" + (relativePath ?? string.Empty).Trim('/');
    }

    private async Task BrowseFolderAsync()
    {
        var result = await _owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Choose local sync folder", AllowMultiple = false });

        if (result.Count == 0)
            return;

        var path = result[0].TryGetLocalPath() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
            return;

        LocalFolderPath = path;
        RemoteFolderName = DeriveRemoteFolderName(path);
    }

    private async Task ChooseRemoteFolderAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var vm = new FolderBrowserViewModel(_syncManager, _contextId, new SelectiveSyncConfig())
            {
                IsSingleSelect = true,
            };
            var dialog = new FolderBrowserDialog(vm);
            dialog.Show();

            var tcs = new TaskCompletionSource();
            dialog.Closed += (_, _) => tcs.TrySetResult();
            await tcs.Task;

            if (dialog.SelectedNodeId.HasValue)
                SetExistingRemoteFolder(dialog.SelectedNodeId.Value, dialog.SelectedRelativePath);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load remote folders: {ex.Message}";
        }
    }

    private async Task ChooseParentFolderAsync()
    {
        ErrorMessage = string.Empty;
        try
        {
            var vm = new FolderBrowserViewModel(_syncManager, _contextId, new SelectiveSyncConfig())
            {
                IsSingleSelect = true,
            };
            var dialog = new FolderBrowserDialog(vm);
            dialog.Show();

            var tcs = new TaskCompletionSource();
            dialog.Closed += (_, _) => tcs.TrySetResult();
            await tcs.Task;

            if (dialog.SelectedNodeId.HasValue)
                SetParentFolder(dialog.SelectedNodeId.Value, dialog.SelectedRelativePath);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load remote folders: {ex.Message}";
        }
    }

    /// <summary>Validates input, creates or selects the remote folder, then sets <see cref="DialogResult"/>.</summary>
    public async Task ConfirmAsync()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(LocalFolderPath))
        {
            ErrorMessage = "Please choose a local sync folder.";
            return;
        }

        var normalized = Path.GetFullPath(LocalFolderPath);
        foreach (var existing in _existingLocalRoots)
        {
            if (SyncFolderOverlapGuard.PathsOverlap(existing, normalized))
            {
                ErrorMessage = "That folder is already inside another synced folder (or contains one).";
                return;
            }
        }

        Guid remoteNodeId;
        string remoteDisplayPath;

        if (UseExistingRemoteFolder)
        {
            if (_existingRemoteNodeId is not { } existingNodeId)
            {
                ErrorMessage = "Please choose a remote folder.";
                return;
            }

            remoteNodeId = existingNodeId;
            remoteDisplayPath = ExistingRemoteFolderPath;
        }
        else
        {
            var name = RemoteFolderName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ErrorMessage = "Please enter a remote folder name.";
                return;
            }

            if (name.Contains('/') || name.Contains('\\'))
            {
                ErrorMessage = "Remote folder name must not contain '/' or '\\'.";
                return;
            }

            IsBusy = true;
            try
            {
                var created = await _syncManager.CreateRemoteFolderAsync(
                    _contextId, name, _remoteParentNodeId);

                remoteNodeId = created.Id;
                remoteDisplayPath = string.IsNullOrWhiteSpace(RemoteParentPath)
                    ? "/" + name
                    : RemoteParentPath.TrimEnd('/') + "/" + name;
            }
            catch (Exception ex)
            {
                // Duplicate name and other server-side validation errors surface here.
                ErrorMessage = $"Could not create remote folder: {ex.Message}";
                return;
            }
            finally
            {
                IsBusy = false;
            }
        }

        DialogResult = new AddFolderResult(normalized, remoteNodeId, remoteDisplayPath);
    }
}

/// <summary>Result returned by the Add Folder dialog.</summary>
/// <param name="LocalFolderPath">Chosen local folder path.</param>
/// <param name="RemoteFolderNodeId">Remote folder NodeId (never <see cref="Guid.Empty"/>).</param>
/// <param name="RemoteFolderPath">Remote folder display path (e.g. "/Documents").</param>
public sealed record AddFolderResult(string LocalFolderPath, Guid RemoteFolderNodeId, string RemoteFolderPath);
