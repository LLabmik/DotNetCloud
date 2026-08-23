using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.SyncTray.ViewModels;

namespace DotNetCloud.Client.SyncTray.Views;

/// <summary>
/// Dialog for adding an additional local sync folder to an existing account,
/// with an optional remote folder mapping.
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
/// View-model for the Add Folder dialog. Handles the local folder picker,
/// an optional single-select remote folder picker, and overlap validation.
/// </summary>
public sealed class AddFolderDialogViewModel : ViewModelBase
{
    private readonly Window _owner;
    private readonly ISyncContextManager _syncManager;
    private readonly Guid _contextId;
    private readonly IReadOnlyList<string> _existingLocalRoots;

    private string _localFolderPath = string.Empty;
    private string _remoteFolderPath = string.Empty;
    private Guid _remoteFolderNodeId;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private AddFolderResult? _dialogResult;

    /// <summary>Chosen local folder path.</summary>
    public string LocalFolderPath
    {
        get => _localFolderPath;
        set => SetProperty(ref _localFolderPath, value);
    }

    /// <summary>Chosen remote folder path (display), or empty for whole-account sync.</summary>
    public string RemoteFolderPath
    {
        get => _remoteFolderPath;
        set => SetProperty(ref _remoteFolderPath, value);
    }

    /// <summary>Chosen remote folder NodeId, or <see cref="Guid.Empty"/> for whole-account sync.</summary>
    public Guid RemoteFolderNodeId
    {
        get => _remoteFolderNodeId;
        private set => SetProperty(ref _remoteFolderNodeId, value);
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

    /// <summary>Opens the remote folder picker (single-select).</summary>
    public ICommand ChooseRemoteFolderCommand { get; }

    /// <summary>Validates and returns the result.</summary>
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
        ConfirmCommand = new RelayCommand(Confirm);
        CancelCommand = new RelayCommand(() => _owner.Close(null));
    }

    private async Task BrowseFolderAsync()
    {
        var result = await _owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Choose local sync folder", AllowMultiple = false });

        if (result.Count > 0)
            LocalFolderPath = result[0].TryGetLocalPath() ?? string.Empty;
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
            {
                RemoteFolderNodeId = dialog.SelectedNodeId.Value;
                RemoteFolderPath = "/" + (dialog.SelectedRelativePath ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load remote folders: {ex.Message}";
        }
    }

    private void Confirm()
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

        DialogResult = new AddFolderResult(normalized, RemoteFolderNodeId, RemoteFolderPath);
    }
}

/// <summary>Result returned by the Add Folder dialog.</summary>
/// <param name="LocalFolderPath">Chosen local folder path.</param>
/// <param name="RemoteFolderNodeId">Chosen remote folder NodeId (<see cref="Guid.Empty"/> = whole account).</param>
/// <param name="RemoteFolderPath">Chosen remote folder path (display only).</param>
public sealed record AddFolderResult(string LocalFolderPath, Guid RemoteFolderNodeId, string RemoteFolderPath);
