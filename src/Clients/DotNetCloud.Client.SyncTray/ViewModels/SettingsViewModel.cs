using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;
using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.SyncIgnore;
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
    private readonly ISyncIgnoreParser _syncIgnore;
    private readonly ISelectiveSyncConfig _selectiveSync;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly string _localSettingsPath;

    private string _addAccountServerUrl = "https://mint22:15443/";
    private string _addAccountClientId = "dotnetcloud-desktop";
    private string _addAccountError = string.Empty;
    private bool _isAddingAccount;
    private bool _startOnLogin;
    private bool _isMuteChatNotifications;
    private decimal _uploadLimitKbps;
    private decimal _downloadLimitKbps;
    private string? _syncIgnoreRoot;

    // Ignored Files tab state
    private readonly ObservableCollection<string> _userIgnorePatterns = [];
    private string _newIgnorePattern = string.Empty;
    private string _ignoreTestPath = string.Empty;
    private string _ignoreTestResult = string.Empty;

    // Conflicts tab state
    private readonly ObservableCollection<ConflictViewModel> _conflicts = [];
    private readonly ObservableCollection<ConflictViewModel> _conflictHistory = [];
    private bool _isLoadingConflicts;
    private int _selectedConflictsTab; // 0 = active, 1 = history
    private int _selectedSettingsTab;  // outer tab index (0=Accounts, 1=General, 2=Ignored, 3=Transfers, 4=Conflicts)

    // Issue #55: Conflict resolution settings
    private bool _autoResolveEnabled = true;
    private int _newerWinsThresholdMinutes = 5;
    private bool _strategyIdentical = true;
    private bool _strategyFastForward = true;
    private bool _strategyCleanMerge = true;
    private bool _strategyNewerWins = true;
    private bool _strategyAppendOnly = true;

    // Issue #57: Symlink mode setting
    private string _symlinkMode = "ignore";

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

    /// <summary>Whether chat popup notifications are muted in SyncTray.</summary>
    public bool IsMuteChatNotifications
    {
        get => _isMuteChatNotifications;
        set
        {
            if (SetProperty(ref _isMuteChatNotifications, value))
            {
                _trayVm.IsMuteChatNotifications = value;
                _ = PersistLocalSettingsAsync();
            }
        }
    }

    /// <summary>Upload bandwidth limit in KB/s (0 = unlimited).</summary>
    public decimal UploadLimitKbps
    {
        get => _uploadLimitKbps;
        set
        {
            if (SetProperty(ref _uploadLimitKbps, value))
                _ = PersistBandwidthAsync();
        }
    }

    /// <summary>Download bandwidth limit in KB/s (0 = unlimited).</summary>
    public decimal DownloadLimitKbps
    {
        get => _downloadLimitKbps;
        set
        {
            if (SetProperty(ref _downloadLimitKbps, value))
                _ = PersistBandwidthAsync();
        }
    }

    /// <summary>Accounts list exposed from the tray view-model.</summary>
    public IReadOnlyList<AccountViewModel> Accounts => _trayVm.Accounts;

    /// <summary>Exposes the tray view-model for bindings that need it (e.g. conflict badge).</summary>
    public TrayViewModel TrayVm => _trayVm;

    // ── Ignored Files tab ─────────────────────────────────────────────────

    /// <summary>Built-in default ignore patterns (system-level, not editable).</summary>
    public IReadOnlyList<string> BuiltInIgnorePatterns => _syncIgnore.BuiltInPatterns;

    /// <summary>User-defined ignore patterns (editable).</summary>
    public ObservableCollection<string> UserIgnorePatterns => _userIgnorePatterns;

    /// <summary>Pattern typed by the user in the "Add pattern" input.</summary>
    public string NewIgnorePattern
    {
        get => _newIgnorePattern;
        set => SetProperty(ref _newIgnorePattern, value);
    }

    /// <summary>Path entered to preview whether it would be ignored.</summary>
    public string IgnoreTestPath
    {
        get => _ignoreTestPath;
        set
        {
            if (SetProperty(ref _ignoreTestPath, value))
                UpdateIgnoreTestResult();
        }
    }

    /// <summary>Result of the ignore-path test (feedback string for the user).</summary>
    public string IgnoreTestResult
    {
        get => _ignoreTestResult;
        private set => SetProperty(ref _ignoreTestResult, value);
    }

    // ── Conflicts tab ─────────────────────────────────────────────────────

    /// <summary>Active (unresolved) conflict view-models.</summary>
    public ObservableCollection<ConflictViewModel> Conflicts => _conflicts;

    /// <summary>Resolved conflict history view-models (last 30 days).</summary>
    public ObservableCollection<ConflictViewModel> ConflictHistory => _conflictHistory;

    /// <summary>Whether the conflict list is currently loading.</summary>
    public bool IsLoadingConflicts
    {
        get => _isLoadingConflicts;
        private set => SetProperty(ref _isLoadingConflicts, value);
    }

    /// <summary>Currently selected sub-tab index (0 = active, 1 = history).</summary>
    public int SelectedConflictsTab
    {
        get => _selectedConflictsTab;
        set => SetProperty(ref _selectedConflictsTab, value);
    }

    /// <summary>Currently selected outer Settings tab index.</summary>
    public int SelectedSettingsTab
    {
        get => _selectedSettingsTab;
        set => SetProperty(ref _selectedSettingsTab, value);
    }

    // ── Conflict Resolution Settings (Issue #55) ──────────────────────────

    /// <summary>Whether auto-resolution is enabled.</summary>
    public bool AutoResolveEnabled
    {
        get => _autoResolveEnabled;
        set { if (SetProperty(ref _autoResolveEnabled, value)) _ = PersistConflictSettingsAsync(); }
    }

    /// <summary>Newer-wins threshold in minutes.</summary>
    public int NewerWinsThresholdMinutes
    {
        get => _newerWinsThresholdMinutes;
        set { if (SetProperty(ref _newerWinsThresholdMinutes, value)) _ = PersistConflictSettingsAsync(); }
    }

    /// <summary>Whether the "identical" conflict strategy is enabled.</summary>
    public bool StrategyIdentical
    {
        get => _strategyIdentical;
        set { if (SetProperty(ref _strategyIdentical, value)) _ = PersistConflictSettingsAsync(); }
    }

    /// <summary>Whether the "fast-forward" conflict strategy is enabled.</summary>
    public bool StrategyFastForward
    {
        get => _strategyFastForward;
        set { if (SetProperty(ref _strategyFastForward, value)) _ = PersistConflictSettingsAsync(); }
    }

    /// <summary>Whether the "clean-merge" conflict strategy is enabled.</summary>
    public bool StrategyCleanMerge
    {
        get => _strategyCleanMerge;
        set { if (SetProperty(ref _strategyCleanMerge, value)) _ = PersistConflictSettingsAsync(); }
    }

    /// <summary>Whether the "newer-wins" conflict strategy is enabled.</summary>
    public bool StrategyNewerWins
    {
        get => _strategyNewerWins;
        set { if (SetProperty(ref _strategyNewerWins, value)) _ = PersistConflictSettingsAsync(); }
    }

    /// <summary>Whether the "append-only" conflict strategy is enabled.</summary>
    public bool StrategyAppendOnly
    {
        get => _strategyAppendOnly;
        set { if (SetProperty(ref _strategyAppendOnly, value)) _ = PersistConflictSettingsAsync(); }
    }

    // ── Symlink Mode (Issue #57) ──────────────────────────────────────────

    /// <summary>Symlink handling mode: "ignore" or "sync-as-link".</summary>
    public string SymlinkMode
    {
        get => _symlinkMode;
        set { if (SetProperty(ref _symlinkMode, value)) _ = PersistConflictSettingsAsync(); }
    }

    /// <summary>Available symlink mode options for the UI dropdown.</summary>
    public IReadOnlyList<string> SymlinkModeOptions { get; } = ["ignore", "sync-as-link"];

    // ── Commands ──────────────────────────────────────────────────────────

    /// <summary>Opens the Add Account dialog and completes the OAuth2 flow on confirmation.</summary>
    public ICommand ConnectCommand { get; }

    /// <summary>Removes the account whose context ID is passed as the command parameter.</summary>
    public ICommand RemoveAccountCommand { get; }

    /// <summary>Closes the Settings window.</summary>
    public ICommand CloseCommand { get; }

    /// <summary>Adds <see cref="NewIgnorePattern"/> to the user ignore rules and saves.</summary>
    public ICommand AddIgnorePatternCommand { get; }

    /// <summary>Removes the pattern passed as parameter from the user ignore rules and saves.</summary>
    public ICommand RemoveIgnorePatternCommand { get; }

    /// <summary>Opens the <c>.syncignore</c> file in the system's default text editor.</summary>
    public ICommand EditSyncIgnoreFileCommand { get; }

    /// <summary>Refreshes the active conflicts list from SyncService.</summary>
    public ICommand RefreshConflictsCommand { get; }

    /// <summary>Opens the folder browser for a specific account to configure selective sync.</summary>
    public ICommand ChooseFoldersCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────

    /// <summary>Initializes a new <see cref="SettingsViewModel"/>.</summary>
    public SettingsViewModel(
        TrayViewModel trayVm,
        IIpcClient ipc,
        IOAuth2Service oauth2,
        ISyncIgnoreParser syncIgnore,
        ISelectiveSyncConfig selectiveSync,
        ILogger<SettingsViewModel> logger,
        string? localSettingsPath = null)
    {
        _trayVm = trayVm;
        _ipc = ipc;
        _oauth2 = oauth2;
        _syncIgnore = syncIgnore;
        _selectiveSync = selectiveSync;
        _logger = logger;
        _localSettingsPath = localSettingsPath ?? GetDefaultLocalSettingsPath();

        ConnectCommand = new AsyncRelayCommand(BeginAddAccountFlowAsync);
        RemoveAccountCommand = new AsyncRelayCommand<Guid>(id => _trayVm.RemoveAccountAsync(id));
        CloseCommand = new RelayCommand(static () => { /* handled by the view via CloseCommand binding */ });
        AddIgnorePatternCommand = new AsyncRelayCommand(AddIgnorePatternAsync);
        RemoveIgnorePatternCommand = new AsyncRelayCommand<string>(RemoveIgnorePatternAsync);
        EditSyncIgnoreFileCommand = new RelayCommand(OpenSyncIgnoreInEditor);
        RefreshConflictsCommand = new AsyncRelayCommand(RefreshConflictsAsync);
        ChooseFoldersCommand = new AsyncRelayCommand<Guid>(ShowFolderBrowserForAccountAsync);

        // Forward account list changes from the tray view-model.
        _trayVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrayViewModel.Accounts))
                OnPropertyChanged(nameof(Accounts));
        };

        LoadLocalSettings();
        _trayVm.IsMuteChatNotifications = _isMuteChatNotifications;
    }

    // ── Add account flow ──────────────────────────────────────────────────

    /// <summary>
    /// Starts the interactive add-account flow by opening the add-account dialog.
    /// Intended for both Settings UI command and first-run onboarding.
    /// </summary>
    public async Task BeginAddAccountFlowAsync()
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
                scopes: ["openid", "profile", "offline_access", "files:read", "files:write"],
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

            // Offer selective sync folder browser after successful add.
            await ShowFolderBrowserAsync(data.UserId);
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

    // ── Ignored Files tab ─────────────────────────────────────────────────

    /// <summary>
    /// Ensures the sync-ignore parser is initialised from the first configured
    /// account's sync root.  A no-op if already initialised or no accounts exist.
    /// </summary>
    public void EnsureSyncIgnoreInitialized()
    {
        var account = _trayVm.Accounts.FirstOrDefault();
        if (account is null || _syncIgnoreRoot == account.LocalFolderPath)
            return;

        _syncIgnoreRoot = account.LocalFolderPath;
        _syncIgnore.Initialize(_syncIgnoreRoot);
        _userIgnorePatterns.Clear();
        foreach (var p in _syncIgnore.UserPatterns)
            _userIgnorePatterns.Add(p);
        OnPropertyChanged(nameof(BuiltInIgnorePatterns));
        UpdateIgnoreTestResult();
    }

    private async Task AddIgnorePatternAsync()
    {
        var pattern = NewIgnorePattern.Trim();
        if (string.IsNullOrEmpty(pattern) || _userIgnorePatterns.Contains(pattern))
            return;

        _userIgnorePatterns.Add(pattern);
        _syncIgnore.SetUserPatterns(_userIgnorePatterns.ToList());
        NewIgnorePattern = string.Empty;
        UpdateIgnoreTestResult();
        await PersistIgnoreRulesAsync();
    }

    private async Task RemoveIgnorePatternAsync(string pattern)
    {
        if (_userIgnorePatterns.Remove(pattern))
        {
            _syncIgnore.SetUserPatterns(_userIgnorePatterns.ToList());
            UpdateIgnoreTestResult();
            await PersistIgnoreRulesAsync();
        }
    }

    private void OpenSyncIgnoreInEditor()
    {
        if (_syncIgnoreRoot is null) return;
        var path = Path.Combine(_syncIgnoreRoot, ".syncignore");
        // Ensure the file exists so the editor can open it.
        if (!File.Exists(path))
            File.WriteAllText(path, string.Empty);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open .syncignore in system editor.");
        }
    }

    private void UpdateIgnoreTestResult()
    {
        var path = IgnoreTestPath.Trim();
        if (string.IsNullOrEmpty(path))
        {
            IgnoreTestResult = string.Empty;
            return;
        }

        IgnoreTestResult = _syncIgnore.IsIgnored(path)
            ? $"\u2714 \"{path}\" would be ignored."
            : $"\u2718 \"{path}\" would NOT be ignored.";
    }

    private async Task PersistIgnoreRulesAsync()
    {
        if (_syncIgnoreRoot is null) return;
        try
        {
            await _syncIgnore.SaveAsync(_syncIgnoreRoot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save .syncignore.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task PersistBandwidthAsync()
    {
        try
        {
            await _ipc.UpdateBandwidthAsync(_uploadLimitKbps, _downloadLimitKbps);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist bandwidth settings via IPC.");
        }
    }

    /// <summary>
    /// Persists the conflict resolution settings via IPC.
    /// Issue #55: settings are saved to sync-settings.json by the service.
    /// </summary>
    private async Task PersistConflictSettingsAsync()
    {
        try
        {
            var strategies = new List<string>();
            if (_strategyIdentical) strategies.Add("identical");
            if (_strategyFastForward) strategies.Add("fast-forward");
            if (_strategyCleanMerge) strategies.Add("clean-merge");
            if (_strategyNewerWins) strategies.Add("newer-wins");
            if (_strategyAppendOnly) strategies.Add("append-only");

            await _ipc.UpdateConflictSettingsAsync(
                _autoResolveEnabled, _newerWinsThresholdMinutes, strategies);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist conflict resolution settings via IPC.");
        }
    }

    /// <summary>
    /// Shows the folder browser dialog for the most recently added account.
    /// Used after add-account flow completes.
    /// </summary>
    private async Task ShowFolderBrowserAsync(Guid userId)
    {
        // Find the context that matches the just-added user.
        var account = _trayVm.Accounts.FirstOrDefault(a => a.ContextId != Guid.Empty);
        if (account is null) return;
        await ShowFolderBrowserForAccountAsync(account.ContextId);
    }

    /// <summary>
    /// Shows the folder browser dialog for a specific account context.
    /// </summary>
    private async Task ShowFolderBrowserForAccountAsync(Guid contextId)
    {
        try
        {
            var account = _trayVm.Accounts.FirstOrDefault(a => a.ContextId == contextId);
            var configDir = account?.LocalFolderPath ?? string.Empty;
            var configPath = Path.Combine(configDir, ".selective-sync.json");

            await _selectiveSync.LoadAsync(configPath);

            var vm = new FolderBrowserViewModel(_ipc, contextId, _selectiveSync, configPath);
            var dialog = new FolderBrowserDialog(vm);
            dialog.Show();

            var tcs = new TaskCompletionSource();
            dialog.Closed += (_, _) => tcs.TrySetResult();
            await tcs.Task;

            if (dialog.Saved)
            {
                // Trigger re-sync so the engine picks up the new selective sync rules.
                try
                {
                    await _ipc.SyncNowAsync(contextId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to trigger sync-now after folder selection.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open folder browser for context {ContextId}.", contextId);
        }
    }

    private static void ApplyStartOnLogin(bool enable)
    {
        // Platform-specific auto-start registration.
        // Windows: HKCU\Software\Microsoft\Windows\CurrentVersion\Run
        // Linux: $HOME/.config/autostart/dotnetcloud-sync-tray.desktop
        // Deferred to a future platform-packaging phase.
        _ = enable;
    }

    private static string GetDefaultLocalSettingsPath()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DotNetCloud");
        return Path.Combine(root, "sync-tray-settings.json");
    }

    private void LoadLocalSettings()
    {
        try
        {
            if (!File.Exists(_localSettingsPath))
                return;

            using var stream = File.OpenRead(_localSettingsPath);
            var settings = JsonSerializer.Deserialize<SyncTrayLocalSettings>(stream);
            if (settings is null)
                return;

            _isMuteChatNotifications = settings.IsMuteChatNotifications;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load local SyncTray settings from {Path}.", _localSettingsPath);
        }
    }

    private async Task PersistLocalSettingsAsync()
    {
        try
        {
            var settingsDir = Path.GetDirectoryName(_localSettingsPath);
            if (!string.IsNullOrWhiteSpace(settingsDir))
                Directory.CreateDirectory(settingsDir);

            await using var stream = File.Create(_localSettingsPath);
            await JsonSerializer.SerializeAsync(
                stream,
                new SyncTrayLocalSettings { IsMuteChatNotifications = _isMuteChatNotifications },
                new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist local SyncTray settings to {Path}.", _localSettingsPath);
        }
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

    // ── Conflicts tab ─────────────────────────────────────────────────────

    /// <summary>
    /// Refreshes the active conflicts list from SyncService.
    /// Called when the Conflicts tab becomes visible.
    /// </summary>
    public async Task RefreshConflictsAsync()
    {
        IsLoadingConflicts = true;
        try
        {
            _conflicts.Clear();
            _conflictHistory.Clear();

            foreach (var account in _trayVm.Accounts)
            {
                var active = await _ipc.ListConflictsAsync(account.ContextId, includeHistory: false);
                foreach (var r in active)
                    _conflicts.Add(new ConflictViewModel(r, _ipc, _trayVm, _logger));

                var history = await _ipc.ListConflictsAsync(account.ContextId, includeHistory: true);
                foreach (var r in history.Where(h => h.IsResolved))
                    _conflictHistory.Add(new ConflictViewModel(r, _ipc, _trayVm, _logger));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load conflicts.");
        }
        finally
        {
            IsLoadingConflicts = false;
        }
    }
}

internal sealed class SyncTrayLocalSettings
{
    public bool IsMuteChatNotifications { get; init; }
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
