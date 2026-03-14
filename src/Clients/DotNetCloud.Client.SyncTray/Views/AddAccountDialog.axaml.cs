using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DotNetCloud.Client.SyncTray.Views;

/// <summary>
/// Dialog for adding a new DotNetCloud account via OAuth2 PKCE browser flow.
/// </summary>
public partial class AddAccountDialog : Window
{
    private readonly AddAccountDialogViewModel _vm;

    /// <summary>Initializes the dialog with no default server URL (required by Avalonia runtime loader).</summary>
    public AddAccountDialog() : this(string.Empty) { }

    /// <summary>Initializes the dialog with a pre-filled server URL.</summary>
    public AddAccountDialog(string defaultServerUrl)
    {
        InitializeComponent();
        _vm = new AddAccountDialogViewModel(defaultServerUrl, this);
        DataContext = _vm;

        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddAccountDialogViewModel.DialogResult))
        {
            Close(_vm.DialogResult);
        }
    }
}

/// <summary>
/// View-model for the Add Account dialog.  Handles server URL input,
/// folder picker, and OAuth2 sign-in flow initiation.
/// </summary>
public sealed class AddAccountDialogViewModel : ViewModels.ViewModelBase
{
    private readonly Window _owner;
    private string _serverUrl;
    private string _localFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "synctray");
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private AddAccountResult? _dialogResult;

    /// <summary>Server URL entered by the user.</summary>
    public string ServerUrl
    {
        get => _serverUrl;
        set => SetProperty(ref _serverUrl, value);
    }

    /// <summary>Local folder path chosen by the user.</summary>
    public string LocalFolderPath
    {
        get => _localFolderPath;
        set => SetProperty(ref _localFolderPath, value);
    }

    /// <summary>Validation or sign-in error message.</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Whether an async operation (folder browse or sign-in) is in progress.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Non-null when the dialog should close with a result.</summary>
    public AddAccountResult? DialogResult
    {
        get => _dialogResult;
        private set => SetProperty(ref _dialogResult, value);
    }

    /// <summary>Command to open the folder picker.</summary>
    public System.Windows.Input.ICommand BrowseFolderCommand { get; }

    /// <summary>Command to initiate the OAuth2 sign-in.</summary>
    public System.Windows.Input.ICommand SignInCommand { get; }

    /// <summary>Command to cancel the dialog.</summary>
    public System.Windows.Input.ICommand CancelCommand { get; }

    /// <summary>Initializes the view-model.</summary>
    public AddAccountDialogViewModel(string defaultServerUrl, Window owner)
    {
        _serverUrl = defaultServerUrl;
        _owner = owner;

        BrowseFolderCommand = new RelayCommand(async () => await BrowseFolderAsync());
        SignInCommand = new RelayCommand(Confirm, () => !_isBusy);
        CancelCommand = new RelayCommand(() => _owner.Close(null));
    }

    private async Task BrowseFolderAsync()
    {
        var result = await _owner.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "Choose sync folder", AllowMultiple = false });

        if (result.Count > 0)
            LocalFolderPath = result[0].TryGetLocalPath() ?? string.Empty;
    }

    private void Confirm()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            ErrorMessage = "Server URL is required.";
            return;
        }

        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out _))
        {
            ErrorMessage = "Invalid server URL.";
            return;
        }

        if (string.IsNullOrWhiteSpace(LocalFolderPath))
        {
            ErrorMessage = "Please choose a local sync folder.";
            return;
        }

        // Return the inputs — the caller (TrayIconManager / SettingsWindow) will complete OAuth2.
        DialogResult = new AddAccountResult(ServerUrl.TrimEnd('/'), LocalFolderPath);
    }
}

/// <summary>Result returned by the Add Account dialog.</summary>
/// <param name="ServerUrl">Validated server base URL.</param>
/// <param name="LocalFolderPath">Chosen local folder path.</param>
public sealed record AddAccountResult(string ServerUrl, string LocalFolderPath);

/// <summary>Minimal <see cref="System.Windows.Input.ICommand"/> implementation.</summary>
internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Func<Task>? _asyncExecute;
    private readonly Action? _execute;
    private readonly Func<bool>? _canExecute;

    internal RelayCommand(Func<Task> asyncExecute, Func<bool>? canExecute = null)
    {
        _asyncExecute = asyncExecute;
        _canExecute = canExecute;
    }

    internal RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        if (_asyncExecute is not null)
            _ = _asyncExecute();
        else
            _execute?.Invoke();
    }

    /// <summary>Raises <see cref="CanExecuteChanged"/>.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
