# SyncTray: Add-Folder Creates Remote Directory (Same-Named)

> Implementation plan for the desktop SyncTray client (`DotNetCloud.Client.SyncTray`).
> When the user adds a local folder, SyncTray creates a matching directory on the server
> (name pre-populated from the local folder's name, editable), never maps to the whole-account
> root folder, and the Settings window shows all content on the Accounts tab.

## Scope

- **Client-only.** No server, no gRPC, no `Core.Server` changes.
- The server already exposes everything needed:
  - `POST api/v1/files/folders` → `DotNetCloudApiClient.CreateFolderAsync(name, parentId)` (parentId `null` = account root).
  - `POST api/v1/files/sync/folders` → `DotNetCloudApiClient.RegisterSyncFolderAsync(nodeId)` (already invoked automatically by `SyncContextManager.AddFolderAsync` → `TryRegisterSyncFolderOnServerAsync`).

## Decisions (final — do not revisit)

| Topic                    | Decision                                                                                            |
| ------------------------ | --------------------------------------------------------------------------------------------------- |
| Remote mapping mode      | Default = **create a new remote folder**. Keep an **"Use existing remote folder"** alternative.     |
| Remote folder name       | Pre-populated with the local folder's **leaf name**, editable by the user.                          |
| Name collision           | Show the server error **inline** in the dialog and keep it open (user renames). No silent reuse.    |
| New folder parent        | Optional. Default = account root (top level). User may pick a parent folder via the folder browser. |
| Root (whole-account) ban | Remove the old "optional → whole account" default. Existing-folder picker already excludes root.    |
| Settings sizing          | Wrap the Accounts tab in a `ScrollViewer`; enlarge the default window height.                       |

## Background (current behaviour)

- `SyncContextManager.AddFolderAsync(contextId, localFolderPath, serverFolderId, serverFolderDisplayPath)` adds a new folder context and auto-registers it server-side when `serverFolderId` is non-null.
- `AddFolderDialog` currently returns `AddFolderResult(LocalFolderPath, RemoteFolderNodeId, RemoteFolderPath)` where `RemoteFolderNodeId == Guid.Empty` means **whole-account sync** — this is the forbidden root case that must be removed.
- `SettingsViewModel.BeginAddFolderFlowAsync` maps `Guid.Empty` → `null` before calling `AddFolderAsync`.
- The folder browser (`FolderBrowserViewModel` in single-select mode) lists only the **children of root**, so the root node (`NodeId == Guid.Empty`) is never selectable. `SelectFolder` additionally guards `NodeId == Guid.Empty`.
- The Settings `Accounts` tab has **no `ScrollViewer`** (unlike General/Transfers/etc.), so content clips at the fixed 480px window height.

## Repository conventions (MUST follow)

- File-scoped namespaces, nullable enabled, `TreatWarningsAsErrors` (via `Directory.Build.props`).
- XML doc comments on public members (SyncTray csproj also suppresses CS1591, but keep docs anyway).
- `src/Clients/DotNetCloud.Client.SyncTray/Properties/AssemblyInfo.cs` already has `InternalsVisibleTo("DotNetCloud.Client.SyncTray.Tests")` — `internal` test seams are allowed.
- `AddFolderDialog.axaml` does **not** set `x:CompileBindings="False"` but the project has no `AvaloniaUseCompiledBindingsByDefault`, so bindings are **reflection-based**; `{Binding !SomeBool}` and `{Binding SomeBool, Converter={x:Static BoolConverters.Not}}` are both safe (already used in this codebase).

---

## Step 1 — `ISyncContextManager`: add `CreateRemoteFolderAsync`

**File:** `src/Clients/DotNetCloud.Client.Core/Sync/ISyncContextManager.cs`

The file already has `using DotNetCloud.Client.Core.Api;` at the top (required for `FileNodeResponse`).

Find this method (near the end of the interface, before the `// ── Events ──` region):

```csharp
    /// <summary>
    /// Returns the server-side folder tree for the given context (for selective sync UI).
    /// </summary>
    Task<SyncTreeNodeResponse?> GetFolderTreeAsync(
        Guid contextId, CancellationToken cancellationToken = default);
```

Insert the following method **immediately after** it:

```csharp
    /// <summary>
    /// Creates a new remote folder on the server for the given context and returns the created node.
    /// </summary>
    /// <param name="contextId">Sync context ID whose credentials to use.</param>
    /// <param name="name">New folder name (must not contain path separators).</param>
    /// <param name="parentId">Parent folder node ID, or <c>null</c> to create at the account root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<FileNodeResponse> CreateRemoteFolderAsync(
        Guid contextId,
        string name,
        Guid? parentId,
        CancellationToken cancellationToken = default);
```

## Step 2 — `SyncContextManager`: implement it

**File:** `src/Clients/DotNetCloud.Client.Core/Sync/SyncContextManager.cs`

Find `GetFolderTreeAsync` (currently near line 524):

```csharp
    /// <inheritdoc/>
    public async Task<SyncTreeNodeResponse?> GetFolderTreeAsync(
        Guid contextId, CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running?.ApiClient is null)
            return null;

        // The access token may not be set yet if this is called before the first
        // sync pass (e.g. immediately after add-account). Load it from the token store.
        await EnsureAccessTokenAsync(running, cancellationToken);

        return await running.ApiClient.GetFolderTreeAsync(null, cancellationToken);
    }
```

Insert **immediately after** it:

```csharp
    /// <inheritdoc/>
    public async Task<FileNodeResponse> CreateRemoteFolderAsync(
        Guid contextId, string name, Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        var running = await GetRunningContextAsync(contextId);
        if (running?.ApiClient is null)
            throw new InvalidOperationException("Sync context not found or not running.");

        // The access token may not be set yet if this is called outside the sync loop.
        await EnsureAccessTokenAsync(running, cancellationToken);

        return await running.ApiClient.CreateFolderAsync(name, parentId, cancellationToken);
    }
```

No other changes in this file. (`EnsureAccessTokenAsync`, `GetRunningContextAsync`, and `ApiClient.CreateFolderAsync(name, parentId, ct)` all already exist.)

---

## Step 3 — `AddFolderDialog.axaml`: new remote-section UI

**File:** `src/Clients/DotNetCloud.Client.SyncTray/Views/AddFolderDialog.axaml`

Replace the **entire file** with:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:DotNetCloud.Client.SyncTray.Views"
        x:Class="DotNetCloud.Client.SyncTray.Views.AddFolderDialog"
        x:DataType="vm:AddFolderDialogViewModel"
        Title="Add Sync Folder"
        Width="480"
        Height="380"
        WindowStartupLocation="CenterOwner"
        CanResize="False"
        SizeToContent="Height">

    <StackPanel Margin="20" Spacing="12">

        <TextBlock Text="Add a local folder to sync"
                   FontSize="16" FontWeight="SemiBold" />

        <TextBlock Text="Choose a local folder. SyncTray will create a matching folder on the server (or you can pick an existing one)."
                   TextWrapping="Wrap"
                   Foreground="{DynamicResource TextFillColorSecondaryBrush}" />

        <!-- Local folder -->
        <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto" RowSpacing="6">
            <TextBlock Grid.Column="0" Text="Local folder"
                       VerticalAlignment="Center" Margin="0,0,12,0" />
            <Grid Grid.Column="1" ColumnDefinitions="*,Auto">
                <TextBox Grid.Column="0"
                         PlaceholderText="Choose a local folder…"
                         Text="{Binding LocalFolderPath}"
                         IsReadOnly="True" />
                <Button Grid.Column="1" Content="…" Margin="6,0,0,0"
                        Command="{Binding BrowseFolderCommand}" />
            </Grid>
        </Grid>

        <!-- Remote mapping mode -->
        <StackPanel Orientation="Horizontal" Spacing="16">
            <RadioButton Content="Create a new remote folder"
                         GroupName="RemoteMode"
                         IsChecked="{Binding UseExistingRemoteFolder, Converter={x:Static BoolConverters.Not}}" />
            <RadioButton Content="Use an existing remote folder"
                         GroupName="RemoteMode"
                         IsChecked="{Binding UseExistingRemoteFolder}" />
        </StackPanel>

        <!-- Create-new fields -->
        <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto,Auto" RowSpacing="6"
              IsVisible="{Binding !UseExistingRemoteFolder}">
            <TextBlock Grid.Row="0" Grid.Column="0" Text="Remote folder name"
                       VerticalAlignment="Center" Margin="0,0,12,0" />
            <Grid Grid.Row="0" Grid.Column="1" ColumnDefinitions="*,Auto">
                <TextBox Grid.Column="0"
                         Text="{Binding RemoteFolderName}"
                         PlaceholderText="Folder name on the server" />
            </Grid>

            <TextBlock Grid.Row="1" Grid.Column="0" Text="Parent folder"
                       VerticalAlignment="Center" Margin="0,0,12,0" />
            <Grid Grid.Row="1" Grid.Column="1" ColumnDefinitions="*,Auto">
                <TextBox Grid.Column="0"
                         Text="{Binding RemoteParentPath}"
                         IsReadOnly="True"
                         PlaceholderText="(top level)" />
                <Button Grid.Column="1" Content="Choose…" Margin="6,0,0,0"
                        Command="{Binding ChooseParentFolderCommand}" />
            </Grid>
        </Grid>

        <!-- Use-existing field -->
        <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto" RowSpacing="6"
              IsVisible="{Binding UseExistingRemoteFolder}">
            <TextBlock Grid.Column="0" Text="Remote folder"
                       VerticalAlignment="Center" Margin="0,0,12,0" />
            <Grid Grid.Column="1" ColumnDefinitions="*,Auto">
                <TextBox Grid.Column="0"
                         Text="{Binding ExistingRemoteFolderPath}"
                         IsReadOnly="True"
                         PlaceholderText="Choose a folder…" />
                <Button Grid.Column="1" Content="Choose…" Margin="6,0,0,0"
                        Command="{Binding ChooseRemoteFolderCommand}" />
            </Grid>
        </Grid>

        <TextBlock Text="{Binding ErrorMessage}"
                   Foreground="Red" FontSize="11"
                   IsVisible="{Binding ErrorMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
                   TextWrapping="Wrap" />

        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Spacing="8">
            <Button Content="Cancel"
                    IsEnabled="{Binding !IsBusy}"
                    Command="{Binding CancelCommand}" />
            <Button Content="Add Folder"
                    Classes="accent"
                    IsEnabled="{Binding !IsBusy}"
                    Command="{Binding ConfirmCommand}" />
        </StackPanel>

    </StackPanel>
</Window>
```

Notes:

- `BoolConverters.Not` is already used in `FolderBrowserDialog.axaml` — safe.
- The parent field defaults to the `PlaceholderText="(top level)"` when `RemoteParentPath` is empty; the user does not need to pick a parent to create at the root.

## Step 4 — `AddFolderDialog.axaml.cs`: rewrite the view-model

**File:** `src/Clients/DotNetCloud.Client.SyncTray/Views/AddFolderDialog.axaml.cs`

Replace the **entire file** with the content below. The `AddFolderDialog` window partial class is unchanged in behaviour (it still closes on `DialogResult` change); the view-model and the result record change.

```csharp
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
```

Notes:

- `AsyncRelayCommand` takes a `Func<Task>` — `new AsyncRelayCommand(ConfirmAsync)` is correct (same pattern already used in `SettingsViewModel`).
- `SyncFolderOverlapGuard.PathsOverlap`, `SelectiveSyncConfig`, `FolderBrowserViewModel`, and `FolderBrowserDialog` all already exist and are unchanged.
- `Path.TrimEndingDirectorySeparator` is available on .NET 10.

## Step 5 — `SettingsViewModel`: stop mapping `Guid.Empty` → `null`

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`

In `BeginAddFolderFlowAsync` (near line 554), find:

```csharp
            await _syncManager.AddFolderAsync(
                account.ContextId,
                result.LocalFolderPath,
                result.RemoteFolderNodeId == Guid.Empty ? null : result.RemoteFolderNodeId,
                result.RemoteFolderNodeId == Guid.Empty ? null : result.RemoteFolderPath);
```

Replace with:

```csharp
            await _syncManager.AddFolderAsync(
                account.ContextId,
                result.LocalFolderPath,
                result.RemoteFolderNodeId,
                result.RemoteFolderPath);
```

No other changes in this file (the `existingRoots` collection and `RefreshAccountsAsync()` call stay as-is).

## Step 6 — `SettingsWindow.axaml`: scroll + larger default size

**File:** `src/Clients/DotNetCloud.Client.SyncTray/Views/SettingsWindow.axaml`

### 6a. Window size

Find the window attributes near the top:

```xml
        Width="520"
        Height="480"
        MinWidth="400"
        MinHeight="340"
```

Change to:

```xml
        Width="520"
        Height="560"
        MinWidth="400"
        MinHeight="420"
```

### 6b. Wrap the Accounts tab in a `ScrollViewer`

Find the start of the Accounts tab:

```xml
            <!-- ── Accounts ── -->
            <TabItem Header="Accounts">
                <DockPanel Margin="8">
```

Change to:

```xml
            <!-- ── Accounts ── -->
            <TabItem Header="Accounts">
                <ScrollViewer>
                    <DockPanel Margin="8">
```

Find the end of the Accounts tab (immediately before the General tab):

```xml
                </DockPanel>
            </TabItem>

            <!-- ── General ── -->
            <TabItem Header="General">
```

Change to:

```xml
                    </DockPanel>
                </ScrollViewer>
            </TabItem>

            <!-- ── General ── -->
            <TabItem Header="General">
```

Do not change the `General` tab (it already has its own `ScrollViewer`).

---

## Step 7 — Tests

### 7a. New file: `tests/DotNetCloud.Client.SyncTray.Tests/ViewModels/AddFolderDialogViewModelTests.cs`

Create this file (MSTest + Moq, matching the existing test style). The view-model lives in the `DotNetCloud.Client.SyncTray.Views` namespace; `InternalsVisibleTo` is already configured so `internal` members are reachable.

```csharp
using DotNetCloud.Client.Core.Api;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.SyncTray.Views;
using Moq;

namespace DotNetCloud.Client.SyncTray.Tests.ViewModels;

[TestClass]
public sealed class AddFolderDialogViewModelTests
{
    private static AddFolderDialogViewModel CreateVm(
        Mock<ISyncContextManager> syncMock,
        IReadOnlyList<string>? existingRoots = null)
    {
        return new AddFolderDialogViewModel(null!, syncMock.Object, Guid.CreateVersion7(), existingRoots ?? []);
    }

    [TestMethod]
    public void DeriveRemoteFolderName_FromPath_ReturnsLeafName()
    {
        Assert.AreEqual("Docs", AddFolderDialogViewModel.DeriveRemoteFolderName("/home/user/Docs"));
        Assert.AreEqual("Docs", AddFolderDialogViewModel.DeriveRemoteFolderName("/home/user/Docs/"));
    }

    [TestMethod]
    public async Task Confirm_NoLocalFolder_SetsError()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var vm = CreateVm(syncMock);

        await vm.ConfirmAsync();

        Assert.IsNull(vm.DialogResult);
        Assert.IsFalse(string.IsNullOrEmpty(vm.ErrorMessage));
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Confirm_ExistingMode_NoSelection_SetsError()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var vm = CreateVm(syncMock);
        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = true;

        await vm.ConfirmAsync();

        Assert.IsNull(vm.DialogResult);
        Assert.IsFalse(string.IsNullOrEmpty(vm.ErrorMessage));
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Confirm_CreateMode_EmptyName_SetsError()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var vm = CreateVm(syncMock);
        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = false;
        vm.RemoteFolderName = "   ";

        await vm.ConfirmAsync();

        Assert.IsNull(vm.DialogResult);
        Assert.IsFalse(string.IsNullOrEmpty(vm.ErrorMessage));
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task Confirm_CreateMode_CreatesAtRoot_AndSetsResult()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var vm = CreateVm(syncMock);
        var contextId = Guid.CreateVersion7();
        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = false;
        vm.RemoteFolderName = "Docs";

        // Rebuild the VM with a known context ID so the verify below is precise.
        vm = new AddFolderDialogViewModel(null!, syncMock.Object, contextId, []);

        var created = new FileNodeResponse { Id = Guid.CreateVersion7(), Name = "Docs", NodeType = "Folder" };
        syncMock
            .Setup(i => i.CreateRemoteFolderAsync(contextId, "Docs", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        await vm.ConfirmAsync();

        Assert.IsNotNull(vm.DialogResult);
        Assert.AreEqual(created.Id, vm.DialogResult!.RemoteFolderNodeId);
        Assert.AreEqual("/Docs", vm.DialogResult.RemoteFolderPath);
        Assert.AreEqual("/tmp/new-folder", vm.DialogResult.LocalFolderPath);
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(contextId, "Docs", null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Confirm_CreateMode_WithParent_UsesParentAndNestedPath()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var contextId = Guid.CreateVersion7();
        var parentId = Guid.CreateVersion7();
        var vm = new AddFolderDialogViewModel(null!, syncMock.Object, contextId, []);

        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = false;
        vm.RemoteFolderName = "Docs";
        vm.SetParentFolder(parentId, "Work/Project");

        var created = new FileNodeResponse { Id = Guid.CreateVersion7(), Name = "Docs", NodeType = "Folder" };
        syncMock
            .Setup(i => i.CreateRemoteFolderAsync(contextId, "Docs", parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        await vm.ConfirmAsync();

        Assert.IsNotNull(vm.DialogResult);
        Assert.AreEqual("/Work/Project/Docs", vm.DialogResult!.RemoteFolderPath);
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(contextId, "Docs", parentId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task Confirm_CreateMode_DuplicateName_SetsError_KeepsDialogOpen()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var contextId = Guid.CreateVersion7();
        var vm = new AddFolderDialogViewModel(null!, syncMock.Object, contextId, []);

        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = false;
        vm.RemoteFolderName = "Docs";

        syncMock
            .Setup(i => i.CreateRemoteFolderAsync(contextId, "Docs", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("A folder named Docs already exists."));

        await vm.ConfirmAsync();

        Assert.IsNull(vm.DialogResult);
        Assert.IsFalse(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [TestMethod]
    public async Task Confirm_ExistingMode_UsesSelectedNode()
    {
        var syncMock = new Mock<ISyncContextManager>();
        var vm = CreateVm(syncMock);
        var existingId = Guid.CreateVersion7();

        vm.LocalFolderPath = "/tmp/new-folder";
        vm.UseExistingRemoteFolder = true;
        vm.SetExistingRemoteFolder(existingId, "Documents");

        await vm.ConfirmAsync();

        Assert.IsNotNull(vm.DialogResult);
        Assert.AreEqual(existingId, vm.DialogResult!.RemoteFolderNodeId);
        Assert.AreEqual("/Documents", vm.DialogResult.RemoteFolderPath);
        syncMock.Verify(
            i => i.CreateRemoteFolderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
```

Note: in `Confirm_CreateMode_CreatesAtRoot_AndSetsResult`, the first `CreateVm` + assignment is only there to show the property setup; the final `vm` is rebuilt with a known `contextId` for the `Verify`. A cleaner implementer may drop the first instance and just build with the known ID — do whatever reads clearly.

### 7b. Existing tests

No existing test references `AddFolderDialog`/`AddFolderResult`/`BeginAddFolderFlowAsync`, so **no changes** are required to `SettingsViewModelTests.cs` or `TrayViewModelTests.cs`.

---

## Step 8 — Documentation + version bump (repo rules)

After the code builds and tests pass:

1. **Version bump** (client is rebuilt):
   - `/Directory.Build.props` — bump `PatchVersion` (and/or `PreReleaseVersion`) in the `<!-- Versioning -->` property group.
   - `/src/Clients/DotNetCloud.Client.Android/DotNetCloud.Client.Android.csproj` — update `ApplicationDisplayVersion` to match.
2. **Tracking docs** (targeted edits, `✓`/`☐` — never `[x]`/`[ ]`):
   - `docs/IMPLEMENTATION_CHECKLIST.md` — mark the add-folder remote-creation items complete.
   - `docs/MASTER_PROJECT_PLAN.md` — update the Quick Status Summary table and the relevant step's Status/Deliverables/Notes.

---

## Verification

```bash
# 1. Build (must be clean — TreatWarningsAsErrors is on)
dotnet build

# 2. Tests
dotnet test tests/DotNetCloud.Client.SyncTray.Tests/
dotnet test tests/DotNetCloud.Client.Core.Tests/
```

Manual end-to-end (Linux):

```bash
dotnet publish src/Clients/DotNetCloud.Client.SyncTray -c Release -r linux-x64 --self-contained true -o /tmp/synctray-stage
# then copy over the installed client (see repo memory) and relaunch
```

Manual checks:

1. Open Settings → Accounts → **Add Folder**. Pick a local folder → the **Remote folder name** is pre-populated with the local folder's leaf name.
2. Change the name → a folder with that name is created on the server and the local folder syncs to it.
3. Leave parent as "(top level)" → folder created at account root. Pick a parent → folder nested under it.
4. Enter a name that already exists → inline error shown, dialog stays open, user renames.
5. Switch to **"Use an existing remote folder"** → pick a subfolder (the account root is not selectable) → maps correctly.
6. Settings Accounts tab scrolls when many folders are added; every row/button is reachable.

## Gotchas

- Do **not** touch `AccountViewModel.Folders` (it must stay `SetProperty`-backed) — this change does not modify it.
- Do **not** add gRPC or server changes; folder creation + registration are REST and already exist.
- `AddFolderDialog` uses reflection bindings — `{Binding !UseExistingRemoteFolder}` and `BoolConverters.Not` are valid.
- The whole-account default (`RemoteFolderNodeId == Guid.Empty` → `serverFolderId = null`) is intentionally removed; after this change `AddFolderResult.RemoteFolderNodeId` is always a real folder ID.
