using System.Windows.Input;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.SyncService.Ipc;
using DotNetCloud.Client.SyncTray.Ipc;
using DotNetCloud.Client.SyncTray.Views;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Client.SyncTray.ViewModels;

/// <summary>
/// View-model for the Settings window.  Manages account list, add-account
/// OAuth2 flow, and general preferences.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly TrayViewModel _trayVm;
    private readonly IIpcClient _ipc;
    private readonly IOAuth2Service _oauth2;
    private readonly ILogger<SettingsViewModel> _logger;

    private string _addAccountServerUrl = string.Empty;
    private string _addAccountClientId = "dotnetcloud-desktop";
    private string _addAccountError = string.Empty;
    private bool _isAddingAccount;
    private bool _startOnLogin;
    private decimal _uploadLimitKbps;
    private decimal _downloadLimitKbps;

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Server URL entered by the user when adding a new account.</summary>
    public string AddAccountServerUrl
    {
        get => _addAccountServerUrl;
        set => SetProperty(ref _addAccountServerUrl, value);
    }

    /// <summary>OAuth2 client ID for the desktop app.</summary>
    public string AddAccountClientId
    {
        get => _addAccountClientId;
        set => SetProperty(ref _addAccountClientId, value);
    }

    /// <summary>Error message displayed when adding an account fails.</summary>
    public string AddAccountError
    {
        get => _addAccountError;
        set => SetProperty(ref _addAccountError, value);
    }

    /// <summary>Whether the OAuth2 add-account flow is in progress.</summary>
    public bool IsAddingAccount
    {
        get => _isAddingAccount;
        set => SetProperty(ref _isAddingAccount, value);
    }

    /// <summary>Whether SyncTray should start automatically on OS login.</summary>
    public bool StartOnLogin
    {
        get => _startOnLogin;
        set
        {
            if (SetProperty(ref _startOnLogin, value))
                ApplyStartOnLogin(value);
        }
    }

    /// <summary>Upload bandwidth limit in KB/s (0 = unlimited).</summary>
    public decimal UploadLimitKbps
    {
        get => _uploadLimitKbps;
        set => SetProperty(ref _uploadLimitKbps, value);
    }

    /// <summary>Download bandwidth limit in KB/s (0 = unlimited).</summary>
    public decimal DownloadLimitKbps
    {
        get => _downloadLimitKbps;
        set => SetProperty(ref _downloadLimitKbps, value);
    }

    /// <summary>Accounts list exposed from the tray view-model.</summary>
    public IReadOnlyList<AccountViewModel> Accounts => _trayVm.Accounts;

    // ── Commands ──────────────────────────────────────────────────────────

    /// <summary>Opens the Add Account dialog and completes the OAuth2 flow on confirmation.</summary>
    public ICommand ConnectCommand { get; }

    /// <summary>Removes the account whose context ID is passed as the command parameter.</summary>
    public ICommand RemoveAccountCommand { get; }

    /// <summary>Closes the Settings window.</summary>
    public ICommand CloseCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new <see cref="SettingsViewModel"/>.</summary>
    public SettingsViewModel(
        TrayViewModel trayVm,
        IIpcClient ipc,
        IOAuth2Service oauth2,
        ILogger<SettingsViewModel> logger)
    {
        _trayVm = trayVm;
        _ipc = ipc;
        _oauth2 = oauth2;
        _logger = logger;

        ConnectCommand = new AsyncRelayCommand(OpenAddAccountDialogAsync);
        RemoveAccountCommand = new AsyncRelayCommand<Guid>(id => _trayVm.RemoveAccountAsync(id));
        CloseCommand = new RelayCommand(static () => { /* handled by the view via CloseCommand binding */ });

        // Forward account list changes from the tray view-model.
        _trayVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrayViewModel.Accounts))
                OnPropertyChanged(nameof(Accounts));
        };
    }

    // ── Add account flow ──────────────────────────────────────────────────

    private async Task OpenAddAccountDialogAsync()
    {
        var dialog = new AddAccountDialog(AddAccountServerUrl);
        // Show as a standalone window; owner resolution is handled at the call site.
        dialog.Show();
        var tcs = new TaskCompletionSource<AddAccountResult?>();
        dialog.Closed += (_, _) => tcs.TrySetResult(dialog.DataContext is AddAccountDialogViewModel vm ? vm.DialogResult : null);
        var result = await tcs.Task;

        if (result is null) return;

        await AddAccountAsync(result.ServerUrl, result.LocalFolderPath);
    }

    /// <summary>
    /// Starts the OAuth2 PKCE browser-based authorisation flow for the configured
    /// server URL, then sends the resulting tokens to SyncService.
    /// </summary>
    /// <param name="serverUrl">Validated server base URL.</param>
    /// <param name="localFolderPath">Absolute local folder to synchronise.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task AddAccountAsync(
        string serverUrl, string localFolderPath,
        CancellationToken cancellationToken = default)
    {
        AddAccountError = string.Empty;

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            AddAccountError = "Server URL cannot be empty.";
            return;
        }

        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out _))
        {
            AddAccountError = "Invalid server URL.";
            return;
        }

        if (string.IsNullOrWhiteSpace(localFolderPath))
        {
            AddAccountError = "Sync folder cannot be empty.";
            return;
        }

        IsAddingAccount = true;

        try
        {
            var tokens = await _oauth2.AuthorizeAsync(
                serverUrl, AddAccountClientId,
                scopes: ["openid", "profile", "files:read", "files:write"],
                cancellationToken);

            var data = new AddAccountData
            {
                ServerUrl = serverUrl,
                UserId = ExtractUserId(tokens.AccessToken),
                LocalFolderPath = localFolderPath,
                DisplayName = BuildDisplayName(tokens.AccessToken, serverUrl),
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                ExpiresAt = tokens.ExpiresAt,
            };

            await _ipc.AddAccountAsync(data, cancellationToken);

            AddAccountServerUrl = string.Empty;
            await _trayVm.RefreshAccountsAsync();
        }
        catch (OperationCanceledException)
        {
            AddAccountError = "Authentication cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add account for server {Url}.", serverUrl);
            AddAccountError = $"Failed to add account: {ex.Message}";
        }
        finally
        {
            IsAddingAccount = false;
        }
    }

    /// <summary>Removes the account with the specified context ID.</summary>
    public Task RemoveAccountAsync(Guid contextId) => _trayVm.RemoveAccountAsync(contextId);

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void ApplyStartOnLogin(bool enable)
    {
        // Platform-specific auto-start registration.
        // Windows: HKCU\Software\Microsoft\Windows\CurrentVersion\Run
        // Linux: $HOME/.config/autostart/dotnetcloud-sync-tray.desktop
        // Deferred to a future platform-packaging phase.
        _ = enable;
    }

    /// <summary>
    /// Extracts the <c>sub</c> claim (user ID) from a JWT access token.
    /// Returns <see cref="Guid.Empty"/> if the token cannot be parsed.
    /// </summary>
    private static Guid ExtractUserId(string? accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
            return Guid.Empty;

        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2) return Guid.Empty;

            var payload = parts[1];
            var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("sub", out var sub) &&
                Guid.TryParse(sub.GetString(), out var userId))
            {
                return userId;
            }
        }
        catch
        {
            // Ignore — return empty guid
        }

        return Guid.Empty;
    }

    private static string BuildDisplayName(string? accessToken, string serverUrl)
    {
        string username = "user";

        if (!string.IsNullOrEmpty(accessToken))
        {
            try
            {
                var parts = accessToken.Split('.');
                if (parts.Length >= 2)
                {
                    var padded = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
                    var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
                    using var doc = System.Text.Json.JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("preferred_username", out var u))
                        username = u.GetString() ?? username;
                    else if (doc.RootElement.TryGetProperty("email", out var e))
                        username = e.GetString() ?? username;
                }
            }
            catch
            {
                // Ignore
            }
        }

        if (Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
            return $"{username} @ {uri.Host}";

        return $"{username} @ {serverUrl}";
    }
}

// ── Command helpers ────────────────────────────────────────────────────────────

/// <summary>Simple synchronous relay command.</summary>
internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

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
    public void Execute(object? parameter) => _execute();

    /// <summary>Raises <see cref="CanExecuteChanged"/>.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Async relay command (parameterless).</summary>
internal sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private bool _isRunning;

    internal AsyncRelayCommand(Func<Task> execute) => _execute = execute;

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => !_isRunning;

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        _ = ExecuteAsync();
    }

    private async Task ExecuteAsync()
    {
        _isRunning = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await _execute(); }
        finally
        {
            _isRunning = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>Async relay command with typed parameter.</summary>
internal sealed class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T, Task> _execute;

    internal AsyncRelayCommand(Func<T, Task> execute) => _execute = execute;

#pragma warning disable CS0067 // Event is never used — required by ICommand contract
    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        if (parameter is T typedParam)
            _ = _execute(typedParam);
    }
}
