# SyncTray: Show & Manage Subscribed Sync Folders

> Implementation plan for the desktop SyncTray client (`DotNetCloud.Client.SyncTray`).
> Lists **all** subscribed local sync folders under the account, lets the user
> open / configure / remove each folder, shows per-folder live sync state, and
> prevents the **default (whole-account)** folder from being removed.

## Background (current behaviour)

- Multiple local folders per account are already supported: each folder is its own
  `SyncContextRegistration` / sync context sharing one `AccountKey`
  (`SyncContextManager.AddFolderAsync` copies the account's tokens).
- `TrayViewModel` keeps **one `AccountViewModel` per context** (keyed by `ctx.Id`).
  The tray's "Open sync folder" menu already shows one entry per folder.
- **Gap:** the Settings "Accounts" tab binds to `PrimaryAccount` (the first context only),
  and `AccountViewModel.Folders` hard-codes a single `SyncFolderViewModel` from its own
  registration. So extra folders are invisible and cannot be removed individually.
- `RemoveAccountCommand` currently removes only the primary folder's context, which would
  orphan other folders.

## Decisions (final — do not revisit)

| Topic                     | Decision                                                                                                                                                                                                                                                                                      |
| ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Default folder identity   | The account's **whole-account folder** — the context whose `SyncContextRegistration.ServerFolderId == null`. Defensive tie-break: earliest `RegisteredAt` if more than one null context ever exists.                                                                                          |
| Removing a folder         | Leave local files **and** the per-context `state.db`/`DataDirectory` in place. Only stop the engine and remove the registration (existing `SyncContextManager.RemoveContextAsync` already does exactly this; it also deletes the context's token copy — keep that, it is credential cleanup). |
| "Remove Account" button   | Removes the **entire account** — every folder context.                                                                                                                                                                                                                                        |
| Folder row content        | Local path, remote mapping, live sync state, "(default)" badge, and **Open / Choose Folders / Remove** buttons.                                                                                                                                                                               |
| Default-folder protection | Enforced in the UI (Remove button disabled) **and** in `TrayViewModel.RemoveFolderAsync` (defensive re-check). Core `SyncContextManager` stays generic (no UI policy).                                                                                                                        |

## Repository conventions (MUST follow)

- File-scoped namespaces, nullable enabled, `TreatWarningsAsErrors` (via `Directory.Build.props`).
- XML doc comments on all public members.
- This change is **client-only** (SyncTray). No server, no gRPC, no `Core.Server` changes.

---

## Step 1 — `SyncFolderViewModel`: add `State` + `IsDefault`

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/AccountViewModel.cs`

The file contains **two** classes (`AccountViewModel` then `SyncFolderViewModel`). Replace the
`SyncFolderViewModel` class with the version below (adds a settable `State` and a readonly
`IsDefault`, and changes the constructor to take `bool isDefault`):

```csharp
/// <summary>
/// View-model for a single local sync folder shown under an account.
/// </summary>
public sealed class SyncFolderViewModel : ViewModelBase
{
    private string _state = "Idle";

    /// <summary>Unique context identifier (matches the sync context ID).</summary>
    public Guid ContextId { get; }

    /// <summary>Absolute local sync folder path.</summary>
    public string LocalFolderPath { get; }

    /// <summary>Remote folder display path, or <c>"Whole account"</c> when not scoped.</summary>
    public string RemoteFolderPath { get; }

    /// <summary>Whether this is the account's default (whole-account) folder. Default folders cannot be removed.</summary>
    public bool IsDefault { get; }

    /// <summary>Current sync state string (e.g. <c>Idle</c>, <c>Syncing</c>, <c>Error</c>).</summary>
    public string State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    /// <summary>Initializes a new <see cref="SyncFolderViewModel"/> from a registration.</summary>
    /// <param name="registration">The persisted sync context registration.</param>
    /// <param name="isDefault">Whether this folder is the account's default (whole-account) folder.</param>
    public SyncFolderViewModel(SyncContextRegistration registration, bool isDefault)
    {
        ContextId = registration.Id;
        LocalFolderPath = registration.LocalFolderPath;
        RemoteFolderPath = string.IsNullOrWhiteSpace(registration.ServerFolderDisplayPath)
            ? "Whole account"
            : registration.ServerFolderDisplayPath;
        IsDefault = isDefault;
    }
}
```

## Step 2 — `AccountViewModel`: make `Folders` settable

**Same file:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/AccountViewModel.cs`

Change the `Folders` property from get-only to settable, and stop creating the single-item list
in the constructor (the list is now populated by `TrayViewModel`):

**Before:**

```csharp
    /// <summary>The local sync folders under this account (one per sync context).</summary>
    public IReadOnlyList<SyncFolderViewModel> Folders { get; }
```

**After:**

```csharp
    /// <summary>The local sync folders under this account (one per sync context).</summary>
    public IReadOnlyList<SyncFolderViewModel> Folders { get; set; } = [];
```

**Before (constructor body):**

```csharp
        _state = "Idle";
        Folders = [new SyncFolderViewModel(registration)];
```

**After (constructor body):**

```csharp
        _state = "Idle";
```

> Leave `ContextId`, `DisplayName`, `ServerBaseUrl`, `LocalFolderPath`, `State`, and the rest of
> `AccountViewModel` untouched — the tray "Open sync folder" menu still relies on
> `LocalFolderPath`, and push-event handlers still key on `ContextId`.

---

## Step 3 — `TrayViewModel`: group folders, track per-folder state

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`

### 3a. Add a field (next to `_accounts` / `_accountList`)

```csharp
    // Keyed by context ID for O(1) lookup on push events.
    private readonly Dictionary<Guid, AccountViewModel> _accounts = [];
    private readonly List<AccountViewModel> _accountList = [];
    // Folder row view-models keyed by context ID (mirrors _accounts for the Settings folder list).
    private readonly Dictionary<Guid, SyncFolderViewModel> _folderVmByContext = [];
```

### 3b. Rewrite `UpdateAccounts` (currently ~line 893)

Replace the whole method so it groups contexts by `AccountKey`, builds one
`SyncFolderViewModel` per context (marking the whole-account folder as default), assigns the
group's folder list to every context's `AccountViewModel`, and maintains `_folderVmByContext`:

```csharp
    private void UpdateAccounts(IReadOnlyList<SyncContextRegistration> contexts)
    {
        var seen = new HashSet<Guid>();

        // One account may own several folder contexts; expose the full folder list on each.
        _folderVmByContext.Clear();

        var groups = contexts
            .GroupBy(c => c.AccountKey, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var ordered = group
                .OrderBy(c => c.RegisteredAt)
                .ThenBy(c => c.Id)
                .ToList();

            // Default folder = the whole-account folder (ServerFolderId == null).
            // Tie-break (defensive): earliest RegisteredAt if multiple null contexts exist.
            var defaultRegistration = ordered.FirstOrDefault(c => c.ServerFolderId is null)
                ?? ordered.FirstOrDefault();
            var defaultId = defaultRegistration?.Id;

            var folderVms = ordered
                .Select(c => new SyncFolderViewModel(c, c.Id == defaultId))
                .ToList();

            foreach (var ctx in ordered)
            {
                seen.Add(ctx.Id);
                if (!_accounts.TryGetValue(ctx.Id, out var accountVm))
                {
                    accountVm = new AccountViewModel(ctx);
                    _accounts[ctx.Id] = accountVm;
                    _accountList.Add(accountVm);
                }

                accountVm.Folders = folderVms;
                _folderVmByContext[ctx.Id] = folderVms.First(f => f.ContextId == ctx.Id);
            }
        }

        // Remove accounts no longer present.
        var removed = _accounts.Keys.Except(seen).ToList();
        foreach (var id in removed)
        {
            if (_accounts.Remove(id, out var vm))
                _accountList.Remove(vm);
        }

        OnPropertyChanged(nameof(Accounts));
        UpdateAggregateState();
    }
```

### 3c. Update `RefreshAccountsAsync` (currently ~line 295)

In the per-context status loop, after updating the `AccountViewModel`, also set the folder
view-model's state. The loop currently looks like:

```csharp
            foreach (var ctx in contexts)
            {
                var status = await _syncManager.GetStatusAsync(ctx.Id);
                if (status is not null && _accounts.TryGetValue(ctx.Id, out var vm))
                {
                    vm.State = status.State.ToString();
                    vm.PendingUploads = status.PendingUploads;
                    vm.PendingDownloads = status.PendingDownloads;
                    vm.LastSyncedAt = status.LastSyncedAt;
                    vm.LastError = status.LastError;
                }
            }
```

Add the folder-state update **inside the same `if` block**, after `vm.LastError = ...;`:

```csharp
                    vm.LastError = status.LastError;

                    if (_folderVmByContext.TryGetValue(ctx.Id, out var folderVm))
                        folderVm.State = status.State.ToString();
```

### 3d. Add a state helper + update push handlers

Add this private helper (e.g. just above `UpdateAccounts`):

```csharp
    private void SetFolderState(Guid contextId, string state)
    {
        if (_folderVmByContext.TryGetValue(contextId, out var folderVm))
            folderVm.State = state;
    }
```

Then add one line to each handler that changes account state:

- **`OnSyncProgress`** — after `vm.State = stateStr;` add:
  ```csharp
            SetFolderState(e.ContextId, stateStr);
  ```
- **`OnSyncComplete`** — after `vm.State = "Idle";` add:
  ```csharp
            SetFolderState(e.ContextId, "Idle");
  ```
- **`OnSyncError`** — after `vm.State = "Error";` add:
  ```csharp
            SetFolderState(e.ContextId, "Error");
  ```

> `OnTransferComplete` does not change state — no change needed there.

---

## Step 4 — `TrayViewModel`: remove-folder + remove-account

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`

Replace the existing `RemoveAccountAsync(Guid contextId)` method (currently ~line 383) with the
two methods below.

```csharp
    /// <summary>
    /// Removes a single (non-default) folder and its sync context. The default
    /// whole-account folder is never removable and this method refuses it.
    /// </summary>
    public async Task RemoveFolderAsync(Guid contextId)
    {
        if (_folderVmByContext.TryGetValue(contextId, out var folderVm) && folderVm.IsDefault)
        {
            _logger.LogWarning("Refusing to remove the default (whole-account) folder {ContextId}.", contextId);
            return;
        }

        try
        {
            await _syncManager.RemoveContextAsync(contextId);
            // Rebuild account/folder lists from the remaining contexts (also refreshes state).
            await RefreshAccountsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove folder {Id}.", contextId);
        }
    }

    /// <summary>Removes the entire account (all folder contexts).</summary>
    public async Task RemoveAccountAsync()
    {
        var ids = _accountList.Select(a => a.ContextId).ToList();
        foreach (var contextId in ids)
        {
            try
            {
                await _syncManager.RemoveContextAsync(contextId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove context {Id} while removing account.", contextId);
            }
        }

        _accounts.Clear();
        _accountList.Clear();
        _folderVmByContext.Clear();
        UpdateAggregateState();
        OnPropertyChanged(nameof(Accounts));
    }
```

> `RefreshAccountsAsync` already re-fetches contexts via `GetContextsAsync()`, rebuilds
> `_accounts`/`_folderVmByContext`, and raises `OnPropertyChanged(nameof(Accounts))`, so the
> Settings UI refreshes automatically after a folder removal.

---

## Step 5 — `SettingsViewModel`: new/updated commands

**File:** `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`

### 5a. Declare the new command (near the other `ICommand` properties, ~line 480)

```csharp
    /// <summary>Removes the subscribed folder whose context ID is passed as the command parameter.</summary>
    public ICommand RemoveFolderCommand { get; }
```

### 5b. Wire it in the constructor (near `RemoveAccountCommand = ...`, ~line 513)

**Before:**

```csharp
        RemoveAccountCommand = new AsyncRelayCommand<Guid>(id => _trayVm.RemoveAccountAsync(id));
```

**After:**

```csharp
        RemoveAccountCommand = new AsyncRelayCommand(() => _trayVm.RemoveAccountAsync());
        RemoveFolderCommand = new AsyncRelayCommand<Guid>(_trayVm.RemoveFolderAsync);
```

### 5c. Update the public `RemoveAccountAsync` shim (currently ~line 750)

**Before:**

```csharp
    /// <summary>Removes the account with the specified context ID.</summary>
    public Task RemoveAccountAsync(Guid contextId) => _trayVm.RemoveAccountAsync(contextId);
```

**After:**

```csharp
    /// <summary>Removes the account (all of its folders).</summary>
    public Task RemoveAccountAsync() => _trayVm.RemoveAccountAsync();
```

---

## Step 6 — `SettingsWindow.axaml`: folder list + actions

**File:** `src/Clients/DotNetCloud.Client.SyncTray/Views/SettingsWindow.axaml`

The window sets `x:CompileBindings="False"`, so reflection bindings work — `{Binding !IsDefault}`
and `StringFormat` are safe (both are already used elsewhere in this file).

### 6a. Delete the redundant single "Sync folder" block

Remove the entire `<!-- Sync folder -->` `StackPanel` (it binds `LocalFolderPath` directly and is
now covered by the per-folder list). It is the block starting with:

```xml
                                        <!-- Sync folder -->
                                        <StackPanel Spacing="4">
                                            <TextBlock Text="Sync Folder"
```

and ending with its closing `</StackPanel>` (just before `<!-- Synced folders -->`).

### 6b. Replace the "Synced folders" `ItemsControl`

Replace the current `<!-- Synced folders -->` block (the whole `StackPanel` containing the
`ItemsControl`) with:

```xml
                                        <!-- Synced folders -->
                                        <StackPanel Spacing="4">
                                            <TextBlock Text="Synced Folders"
                                                       FontSize="12"
                                                       FontWeight="SemiBold"
                                                       Foreground="#9AACCA" />
                                            <ItemsControl ItemsSource="{Binding Folders}">
                                                <ItemsControl.ItemTemplate>
                                                    <DataTemplate DataType="vm:SyncFolderViewModel">
                                                        <Grid ColumnDefinitions="*,Auto,Auto,Auto" ColumnSpacing="6" Margin="0,3">
                                                            <StackPanel Grid.Column="0" Spacing="1" VerticalAlignment="Center">
                                                                <StackPanel Orientation="Horizontal" Spacing="6">
                                                                    <TextBlock Text="{Binding LocalFolderPath}"
                                                                               FontSize="12"
                                                                               FontFamily="Consolas, Courier New, monospace"
                                                                               Foreground="#C8D4E8"
                                                                               TextTrimming="CharacterEllipsis"
                                                                               ToolTip.Tip="{Binding LocalFolderPath}" />
                                                                    <Border IsVisible="{Binding IsDefault}"
                                                                            Background="#0E6CB8"
                                                                            CornerRadius="3"
                                                                            Padding="5,1"
                                                                            VerticalAlignment="Center">
                                                                        <TextBlock Text="default" FontSize="10" Foreground="#FFFFFF" />
                                                                    </Border>
                                                                </StackPanel>
                                                                <TextBlock Text="{Binding RemoteFolderPath}"
                                                                           FontSize="11"
                                                                           Foreground="#7A8BA4" />
                                                                <TextBlock Text="{Binding State, StringFormat='State: {0}'}"
                                                                           FontSize="11"
                                                                           Foreground="#6B8AAB" />
                                                            </StackPanel>
                                                            <Button Grid.Column="1"
                                                                    Content="Open"
                                                                    VerticalAlignment="Center"
                                                                    Command="{Binding $parent[Window].DataContext.OpenSyncFolderCommand}"
                                                                    CommandParameter="{Binding LocalFolderPath}" />
                                                            <Button Grid.Column="2"
                                                                    Content="Choose Folders"
                                                                    VerticalAlignment="Center"
                                                                    Command="{Binding $parent[Window].DataContext.ChooseFoldersCommand}"
                                                                    CommandParameter="{Binding ContextId}" />
                                                            <Button Grid.Column="3"
                                                                    Content="Remove"
                                                                    Classes="danger"
                                                                    VerticalAlignment="Center"
                                                                    IsEnabled="{Binding !IsDefault}"
                                                                    Command="{Binding $parent[Window].DataContext.RemoveFolderCommand}"
                                                                    CommandParameter="{Binding ContextId}" />
                                                        </Grid>
                                                    </DataTemplate>
                                                </ItemsControl.ItemTemplate>
                                            </ItemsControl>
                                        </StackPanel>
```

### 6c. Update the account actions row

Remove the account-level "Choose Folders" button (it is now per-folder) and drop the
`CommandParameter` from "Remove Account":

**Before:**

```xml
                                        <StackPanel Orientation="Horizontal" Spacing="8"
                                                    HorizontalAlignment="Right">
                                            <Button Content="Add Folder"
                                                    Command="{Binding $parent[Window].DataContext.AddFolderCommand}" />
                                            <Button Content="Choose Folders"
                                                    Command="{Binding $parent[Window].DataContext.ChooseFoldersCommand}"
                                                    CommandParameter="{Binding ContextId}" />
                                            <Button Content="Remove Account"
                                                    Classes="danger"
                                                    Command="{Binding $parent[Window].DataContext.RemoveAccountCommand}"
                                                    CommandParameter="{Binding ContextId}" />
                                        </StackPanel>
```

**After:**

```xml
                                        <StackPanel Orientation="Horizontal" Spacing="8"
                                                    HorizontalAlignment="Right">
                                            <Button Content="Add Folder"
                                                    Command="{Binding $parent[Window].DataContext.AddFolderCommand}" />
                                            <Button Content="Remove Account"
                                                    Classes="danger"
                                                    Command="{Binding $parent[Window].DataContext.RemoveAccountCommand}" />
                                        </StackPanel>
```

---

## Step 7 — Tests

### 7a. `tests/DotNetCloud.Client.SyncTray.Tests/ViewModels/TrayViewModelTests.cs`

> ⚠️ The existing `SeedAccountAsync` helper gives each context a **unique** `AccountKey`
> (`$"test-{contextId}"`). Multi-folder tests must share one `AccountKey`, so write those tests
> inline with `GetContextsAsync` returning multiple registrations with the same `AccountKey`.

Add the following tests (place before the `BuildVm` helper). Reuse `BuildVm()` and
`SyncContextRegistration` objects; `GetStatusAsync` can be set up with
`It.IsAny<Guid>()` returning `new SyncStatus { State = SyncState.Idle }`.

```csharp
    // ── Folder list & removal ─────────────────────────────────────────────

    [TestMethod]
    public async Task UpdateAccounts_MultipleFoldersSameAccount_GroupsFolders()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var id1 = Guid.CreateVersion7();
        var id2 = Guid.CreateVersion7();

        syncMock.Setup(s => s.GetContextsAsync()).ReturnsAsync([
            new SyncContextRegistration { Id = id1, DisplayName = "A", ServerBaseUrl = "https://cloud.example.com", LocalFolderPath = "/sync/default", UserId = Guid.CreateVersion7(), AccountKey = "acct", OsUserName = "u", DataDirectory = "/tmp/d1", RegisteredAt = DateTime.UtcNow.AddMinutes(-5) },
            new SyncContextRegistration { Id = id2, DisplayName = "A", ServerBaseUrl = "https://cloud.example.com", LocalFolderPath = "/sync/extra", UserId = Guid.CreateVersion7(), AccountKey = "acct", OsUserName = "u", DataDirectory = "/tmp/d2", ServerFolderId = Guid.CreateVersion7(), ServerFolderDisplayPath = "/Documents", RegisteredAt = DateTime.UtcNow },
        ]);
        syncMock.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(new SyncStatus { State = SyncState.Idle });

        await vm.RefreshAccountsAsync();

        var account = vm.Accounts.FirstOrDefault(a => a.ContextId == id1);
        Assert.IsNotNull(account);
        Assert.AreEqual(2, account!.Folders.Count);
        Assert.IsTrue(account.Folders.First(f => f.ContextId == id1).IsDefault);
        Assert.IsFalse(account.Folders.First(f => f.ContextId == id2).IsDefault);
        // Both sibling AccountViewModels share the same folder list.
        var sibling = vm.Accounts.First(a => a.ContextId == id2);
        Assert.AreEqual(2, sibling.Folders.Count);
    }

    [TestMethod]
    public async Task RemoveFolder_DefaultFolder_Refused()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var defaultId = Guid.CreateVersion7();

        // Whole-account folder: ServerFolderId == null.
        syncMock.Setup(s => s.GetContextsAsync()).ReturnsAsync([
            new SyncContextRegistration { Id = defaultId, DisplayName = "A", ServerBaseUrl = "https://cloud.example.com", LocalFolderPath = "/sync", UserId = Guid.CreateVersion7(), AccountKey = "acct", OsUserName = "u", DataDirectory = "/tmp/d1" },
        ]);
        syncMock.Setup(s => s.GetStatusAsync(defaultId)).ReturnsAsync(new SyncStatus { State = SyncState.Idle });

        await vm.RefreshAccountsAsync();
        await vm.RemoveFolderAsync(defaultId);

        syncMock.Verify(s => s.RemoveContextAsync(defaultId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RemoveFolder_NonDefault_RemovesContext()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var defaultId = Guid.CreateVersion7();
        var extraId = Guid.CreateVersion7();

        var twoFolders = new List<SyncContextRegistration>
        {
            new() { Id = defaultId, DisplayName = "A", ServerBaseUrl = "https://cloud.example.com", LocalFolderPath = "/sync/default", UserId = Guid.CreateVersion7(), AccountKey = "acct", OsUserName = "u", DataDirectory = "/tmp/d1" },
            new() { Id = extraId, DisplayName = "A", ServerBaseUrl = "https://cloud.example.com", LocalFolderPath = "/sync/extra", UserId = Guid.CreateVersion7(), AccountKey = "acct", OsUserName = "u", DataDirectory = "/tmp/d2", ServerFolderId = Guid.CreateVersion7(), ServerFolderDisplayPath = "/Documents" },
        };
        var oneFolder = new List<SyncContextRegistration> { twoFolders[0] };

        // First call (RefreshAccounts) returns both; second call (post-removal refresh) returns one.
        syncMock.SetupSequence(s => s.GetContextsAsync())
            .ReturnsAsync(twoFolders)
            .ReturnsAsync(oneFolder);
        syncMock.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(new SyncStatus { State = SyncState.Idle });
        syncMock.Setup(s => s.RemoveContextAsync(extraId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.RefreshAccountsAsync();
        await vm.RemoveFolderAsync(extraId);

        syncMock.Verify(s => s.RemoveContextAsync(extraId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.IsFalse(vm.Accounts.Any(a => a.ContextId == extraId));
    }

    [TestMethod]
    public async Task RemoveAccount_MultipleFolders_RemovesAllContexts()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var id1 = Guid.CreateVersion7();
        var id2 = Guid.CreateVersion7();

        syncMock.Setup(s => s.GetContextsAsync()).ReturnsAsync([
            new SyncContextRegistration { Id = id1, DisplayName = "A", ServerBaseUrl = "https://cloud.example.com", LocalFolderPath = "/sync/1", UserId = Guid.CreateVersion7(), AccountKey = "acct", OsUserName = "u", DataDirectory = "/tmp/d1" },
            new SyncContextRegistration { Id = id2, DisplayName = "A", ServerBaseUrl = "https://cloud.example.com", LocalFolderPath = "/sync/2", UserId = Guid.CreateVersion7(), AccountKey = "acct", OsUserName = "u", DataDirectory = "/tmp/d2", ServerFolderId = Guid.CreateVersion7(), ServerFolderDisplayPath = "/Docs" },
        ]);
        syncMock.Setup(s => s.GetStatusAsync(It.IsAny<Guid>())).ReturnsAsync(new SyncStatus { State = SyncState.Idle });
        syncMock.Setup(s => s.RemoveContextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await vm.RefreshAccountsAsync();
        await vm.RemoveAccountAsync();

        syncMock.Verify(s => s.RemoveContextAsync(id1, It.IsAny<CancellationToken>()), Times.Once);
        syncMock.Verify(s => s.RemoveContextAsync(id2, It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(0, vm.Accounts.Count);
    }

    [TestMethod]
    public async Task RefreshAccounts_UpdatesFolderState()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var id = Guid.CreateVersion7();

        syncMock.Setup(s => s.GetContextsAsync()).ReturnsAsync([
            new SyncContextRegistration { Id = id, DisplayName = "A", ServerBaseUrl = "https://cloud.example.com", LocalFolderPath = "/sync", UserId = Guid.CreateVersion7(), AccountKey = "acct", OsUserName = "u", DataDirectory = "/tmp/d1" },
        ]);
        syncMock.Setup(s => s.GetStatusAsync(id)).ReturnsAsync(new SyncStatus { State = SyncState.Syncing });

        await vm.RefreshAccountsAsync();

        var folder = vm.Accounts.First(a => a.ContextId == id).Folders.First();
        Assert.AreEqual("Syncing", folder.State);
    }
```

### 7b. `tests/DotNetCloud.Client.SyncTray.Tests/ViewModels/SettingsViewModelTests.cs`

Update the existing `RemoveAccountAsync_DelegatesToTrayViewModel` test (currently ~line 183) to
the new no-arg signature:

**Before:**

```csharp
    [TestMethod]
    public async Task RemoveAccountAsync_DelegatesToTrayViewModel()
    {
        var (vm, syncMock, _, _) = BuildVm();
        var contextId = Guid.CreateVersion7();

        syncMock
            .Setup(i => i.RemoveContextAsync(contextId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await vm.RemoveAccountAsync(contextId);

        syncMock.Verify(i => i.RemoveContextAsync(contextId, It.IsAny<CancellationToken>()), Times.Once);
    }
```

**After:**

```csharp
    [TestMethod]
    public async Task RemoveAccountAsync_DelegatesToTrayViewModel()
    {
        var (vm, syncMock, _, _) = BuildVm();

        syncMock
            .Setup(i => i.RemoveContextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await vm.RemoveAccountAsync();

        syncMock.Verify(i => i.RemoveContextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }
```

Optionally add (the `AsyncRelayCommand` is `internal` and fire-and-forget, so assert wiring rather
than calling `Execute`):

```csharp
    [TestMethod]
    public void RemoveFolderCommand_IsWired()
    {
        var (vm, _, _, _) = BuildVm();
        Assert.IsNotNull(vm.RemoveFolderCommand);
    }
```

---

## Complete file inventory

| File                                                                           | Change                                                                                                                                                                                             |
| ------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/AccountViewModel.cs`       | `SyncFolderViewModel` (+`State`, +`IsDefault`, ctor param); `AccountViewModel.Folders` settable                                                                                                    |
| `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/TrayViewModel.cs`          | `_folderVmByContext` field; `UpdateAccounts` grouping; `RefreshAccountsAsync` folder state; `SetFolderState` helper + 3 handler updates; `RemoveFolderAsync`; `RemoveAccountAsync()` (all folders) |
| `src/Clients/DotNetCloud.Client.SyncTray/ViewModels/SettingsViewModel.cs`      | `RemoveFolderCommand`; `RemoveAccountCommand` rewire; public `RemoveAccountAsync()` shim                                                                                                           |
| `src/Clients/DotNetCloud.Client.SyncTray/Views/SettingsWindow.axaml`           | folder list rows (state/default badge/Open/Choose Folders/Remove); actions row                                                                                                                     |
| `tests/DotNetCloud.Client.SyncTray.Tests/ViewModels/TrayViewModelTests.cs`     | 5 new tests                                                                                                                                                                                        |
| `tests/DotNetCloud.Client.SyncTray.Tests/ViewModels/SettingsViewModelTests.cs` | update `RemoveAccountAsync_DelegatesToTrayViewModel`; optional `RemoveFolderCommand_IsWired`                                                                                                       |

No changes to `DotNetCloud.Client.Core`, the server, or any `.csproj`.

---

## Verification

1. Build:
   ```bash
   dotnet build
   ```
   (must be clean — `TreatWarningsAsErrors` is on).
2. Test:
   ```bash
   dotnet test tests/DotNetCloud.Client.SyncTray.Tests/
   ```
3. Manual (Linux, `dotnet run` the SyncTray project or run the published client):
   - With one account connected, use "Add Folder" twice to add two extra folders.
   - Confirm the "Synced Folders" list shows all three folders, each with local path, remote
     mapping, live state, and the default folder showing a "default" badge with its Remove button
     disabled.
   - Click "Remove" on a non-default folder → only that folder disappears; its files on disk are
     untouched; the tray "Open sync folder" menu drops that entry.
   - Click "Remove Account" → all folders are removed.

## Gotchas / notes for the implementer

- **Do not** change `SyncContextManager.RemoveContextAsync` — it already does exactly what folder
  removal needs (stop engine, delete tokens, drop registration; leaves files + `state.db`).
- `AsyncRelayCommand.Execute` is fire-and-forget and `internal`; test behaviour via the public
  `TrayViewModel.RemoveFolderAsync` / `RemoveAccountAsync` methods, not the command objects.
- Multi-folder tests must share one `AccountKey` (the `SeedAccountAsync` helper does **not**).
- Use `Moq.SetupSequence` for `GetContextsAsync` when a test calls `RemoveFolderAsync`, because it
  re-fetches contexts after removal.
- `x:CompileBindings="False"` is set on `SettingsWindow`, so `{Binding !IsDefault}` and
  `StringFormat` bindings work (both patterns already exist in the file).
- Keep XML doc comments on all public members (`TreatWarningsAsErrors` enforces this).
- `SettingsViewModel` already re-raises `Accounts`/`PrimaryAccount` when `TrayViewModel.Accounts`
  changes, so the grouped list refreshes automatically after add/remove.
