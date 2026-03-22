using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.SyncIgnore;
using DotNetCloud.Client.SyncTray.Ipc;
using DotNetCloud.Client.SyncTray.Notifications;
using DotNetCloud.Client.SyncTray.Startup;
using DotNetCloud.Client.SyncTray.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Client.SyncTray.Tests.ViewModels;

[TestClass]
public sealed class SettingsViewModelTests
{
    // ── AddAccount validation ─────────────────────────────────────────────

    [TestMethod]
    public async Task AddAccountAsync_EmptyServerUrl_SetsError()
    {
        var (vm, _, _, _) = BuildVm();

        await vm.AddAccountAsync(string.Empty, "/sync");

        Assert.IsFalse(string.IsNullOrEmpty(vm.AddAccountError));
    }

    [TestMethod]
    public void AddAccountServerUrl_DefaultsToCurrentMint22Endpoint()
    {
        var (vm, _, _, _) = BuildVm();

        Assert.AreEqual("https://mint22.kimball.home:5443/", vm.AddAccountServerUrl);
    }

    [TestMethod]
    public async Task AddAccountAsync_InvalidServerUrl_SetsError()
    {
        var (vm, _, _, _) = BuildVm();

        await vm.AddAccountAsync("not-a-url", "/sync");

        Assert.IsFalse(string.IsNullOrEmpty(vm.AddAccountError));
    }

    [TestMethod]
    public async Task AddAccountAsync_EmptyFolder_SetsError()
    {
        var (vm, _, _, _) = BuildVm();

        await vm.AddAccountAsync("https://cloud.example.com", string.Empty);

        Assert.IsFalse(string.IsNullOrEmpty(vm.AddAccountError));
    }

    [TestMethod]
    public async Task AddAccountAsync_ValidInputs_CallsOAuth2AndIpc()
    {
        var (vm, ipcMock, oauth2Mock, _) = BuildVm();

        var tokenInfo = new TokenInfo
        {
            AccessToken = BuildFakeJwt(Guid.NewGuid()),
            RefreshToken = "refresh",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        };

        oauth2Mock
            .Setup(o => o.AuthorizeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenInfo);

        ipcMock
            .Setup(i => i.AddAccountAsync(It.IsAny<DotNetCloud.Client.SyncService.Ipc.AddAccountData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ipcMock
            .Setup(i => i.ListContextsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await vm.AddAccountAsync("https://cloud.example.com", "/tmp/sync");

        Assert.AreEqual(string.Empty, vm.AddAccountError);
        oauth2Mock.Verify(
            o => o.AuthorizeAsync("https://cloud.example.com", It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        ipcMock.Verify(i => i.AddAccountAsync(It.IsAny<DotNetCloud.Client.SyncService.Ipc.AddAccountData>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task AddAccountAsync_ExistingAccount_SetsErrorAndSkipsAddFlow()
    {
        var (vm, ipcMock, oauth2Mock, _) = BuildVm();

        var existingContextId = Guid.NewGuid();
        ipcMock
            .Setup(i => i.ListContextsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DotNetCloud.Client.SyncService.Ipc.ContextInfo
                {
                    Id = existingContextId,
                    DisplayName = "test@example.com @ cloud.example.com",
                    ServerBaseUrl = "https://cloud.example.com",
                    LocalFolderPath = "/tmp/existing-sync",
                    State = "Idle",
                }
            ]);

        await vm.TrayVm.RefreshAccountsAsync();
        Assert.IsTrue(vm.HasAccount);

        await vm.AddAccountAsync("https://another.example.com", "/tmp/new-sync");

        StringAssert.Contains(vm.AddAccountError, "Only one account");
        oauth2Mock.Verify(
            o => o.AuthorizeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        ipcMock.Verify(
            i => i.AddAccountAsync(It.IsAny<DotNetCloud.Client.SyncService.Ipc.AddAccountData>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task AddAccountAsync_OAuth2Throws_SetsError()
    {
        var (vm, _, oauth2Mock, _) = BuildVm();

        oauth2Mock
            .Setup(o => o.AuthorizeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Auth failed"));

        await vm.AddAccountAsync("https://cloud.example.com", "/tmp/sync");

        Assert.IsFalse(string.IsNullOrEmpty(vm.AddAccountError));
        StringAssert.Contains(vm.AddAccountError, "Auth failed");
    }

    [TestMethod]
    public async Task AddAccountAsync_OAuth2Cancelled_SetsError()
    {
        var (vm, _, oauth2Mock, _) = BuildVm();

        oauth2Mock
            .Setup(o => o.AuthorizeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await vm.AddAccountAsync("https://cloud.example.com", "/tmp/sync");

        Assert.IsFalse(string.IsNullOrEmpty(vm.AddAccountError));
        StringAssert.Contains(vm.AddAccountError, "cancelled");
    }

    // ── RemoveAccount ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task RemoveAccountAsync_DelegatesToTrayViewModel()
    {
        var (vm, ipcMock, _, _) = BuildVm();
        var contextId = Guid.NewGuid();

        ipcMock
            .Setup(i => i.RemoveAccountAsync(contextId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await vm.RemoveAccountAsync(contextId);

        ipcMock.Verify(i => i.RemoveAccountAsync(contextId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task IsMuteChatNotifications_PersistsAndLoadsFromLocalSettingsJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnetcloud-sync-tray-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var settingsPath = Path.Combine(tempDir, "sync-tray-settings.json");

        try
        {
            var ipcMock = new Mock<IIpcClient>();
            ipcMock.SetupGet(i => i.IsConnected).Returns(false);

            var chatMock = new Mock<IChatSignalRClient>();
            chatMock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var notifMock = new Mock<INotificationService>();
            var trayVm = new TrayViewModel(ipcMock.Object, chatMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);
            var oauth2Mock = new Mock<IOAuth2Service>();
            var startupManager = new Mock<IDesktopStartupManager>();
            startupManager.Setup(m => m.TryApplyStartOnLogin(It.IsAny<bool>())).Returns(true);

            var vm1 = new SettingsViewModel(
                trayVm,
                ipcMock.Object,
                oauth2Mock.Object,
                new Mock<ISyncIgnoreParser>().Object,
                new Mock<ISelectiveSyncConfig>().Object,
                startupManager.Object,
                NullLogger<SettingsViewModel>.Instance,
                settingsPath);

            vm1.IsMuteChatNotifications = true;
            await Task.Delay(100);

            var vm2 = new SettingsViewModel(
                trayVm,
                ipcMock.Object,
                oauth2Mock.Object,
                new Mock<ISyncIgnoreParser>().Object,
                new Mock<ISelectiveSyncConfig>().Object,
                startupManager.Object,
                NullLogger<SettingsViewModel>.Instance,
                settingsPath);

            Assert.IsTrue(vm2.IsMuteChatNotifications);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task StartOnLogin_PersistsAndCreatesLinuxAutostartEntry()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnetcloud-sync-startup-tests-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(tempDir, "sync-tray-settings.json");
        var autostartDir = Path.Combine(tempDir, "autostart");
        var trayExecutablePath = Path.Combine(tempDir, "dotnetcloud-sync-tray");

        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(trayExecutablePath, string.Empty);

        try
        {
            var ipcMock = new Mock<IIpcClient>();
            ipcMock.SetupGet(i => i.IsConnected).Returns(false);

            var chatMock = new Mock<IChatSignalRClient>();
            chatMock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var notifMock = new Mock<INotificationService>();
            var trayVm = new TrayViewModel(ipcMock.Object, chatMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);
            var startupManager = new DesktopStartupManager(
                NullLogger<DesktopStartupManager>.Instance,
                trayExecutablePathProvider: () => trayExecutablePath,
                autostartDirectory: autostartDir,
                isLinux: () => true);

            var vm1 = new SettingsViewModel(
                trayVm,
                ipcMock.Object,
                new Mock<IOAuth2Service>().Object,
                new Mock<ISyncIgnoreParser>().Object,
                new Mock<ISelectiveSyncConfig>().Object,
                startupManager,
                NullLogger<SettingsViewModel>.Instance,
                settingsPath);

            vm1.StartOnLogin = true;
            await Task.Delay(100);

            var desktopFilePath = Path.Combine(autostartDir, "dotnetcloud-sync-tray.desktop");
            Assert.IsTrue(File.Exists(desktopFilePath));

            var desktopFileContents = await File.ReadAllTextAsync(desktopFilePath);
            var escapedExecPath = trayExecutablePath
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
            StringAssert.Contains(desktopFileContents, $"Exec=\"{escapedExecPath}\"");

            var vm2 = new SettingsViewModel(
                trayVm,
                ipcMock.Object,
                new Mock<IOAuth2Service>().Object,
                new Mock<ISyncIgnoreParser>().Object,
                new Mock<ISelectiveSyncConfig>().Object,
                startupManager,
                NullLogger<SettingsViewModel>.Instance,
                settingsPath);

            Assert.IsTrue(vm2.StartOnLogin);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task StartOnLogin_DisablingRemovesLinuxAutostartEntry()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnetcloud-sync-startup-tests-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(tempDir, "sync-tray-settings.json");
        var autostartDir = Path.Combine(tempDir, "autostart");
        var trayExecutablePath = Path.Combine(tempDir, "dotnetcloud-sync-tray");

        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(trayExecutablePath, string.Empty);

        try
        {
            var ipcMock = new Mock<IIpcClient>();
            ipcMock.SetupGet(i => i.IsConnected).Returns(false);

            var chatMock = new Mock<IChatSignalRClient>();
            chatMock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var notifMock = new Mock<INotificationService>();
            var trayVm = new TrayViewModel(ipcMock.Object, chatMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);
            var startupManager = new DesktopStartupManager(
                NullLogger<DesktopStartupManager>.Instance,
                trayExecutablePathProvider: () => trayExecutablePath,
                autostartDirectory: autostartDir,
                isLinux: () => true);

            var vm1 = new SettingsViewModel(
                trayVm,
                ipcMock.Object,
                new Mock<IOAuth2Service>().Object,
                new Mock<ISyncIgnoreParser>().Object,
                new Mock<ISelectiveSyncConfig>().Object,
                startupManager,
                NullLogger<SettingsViewModel>.Instance,
                settingsPath);

            vm1.StartOnLogin = true;
            await Task.Delay(100);

            var desktopFilePath = Path.Combine(autostartDir, "dotnetcloud-sync-tray.desktop");
            Assert.IsTrue(File.Exists(desktopFilePath));

            vm1.StartOnLogin = false;
            await Task.Delay(100);

            Assert.IsFalse(File.Exists(desktopFilePath));

            var vm2 = new SettingsViewModel(
                trayVm,
                ipcMock.Object,
                new Mock<IOAuth2Service>().Object,
                new Mock<ISyncIgnoreParser>().Object,
                new Mock<ISelectiveSyncConfig>().Object,
                startupManager,
                NullLogger<SettingsViewModel>.Instance,
                settingsPath);

            Assert.IsFalse(vm2.StartOnLogin);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task StartupManager_EnsuresLinuxApplicationLauncherEntry()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnetcloud-sync-launcher-tests-{Guid.NewGuid():N}");
        var trayExecutablePath = Path.Combine(tempDir, "dotnetcloud-sync-tray");
        var applicationsDir = Path.Combine(tempDir, "applications");

        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(trayExecutablePath, string.Empty);

        try
        {
            var startupManager = new DesktopStartupManager(
                NullLogger<DesktopStartupManager>.Instance,
                trayExecutablePathProvider: () => trayExecutablePath,
                applicationsDirectory: applicationsDir,
                isLinux: () => true);

            var created = startupManager.TryEnsureApplicationLauncher();
            Assert.IsTrue(created);

            var launcherPath = Path.Combine(applicationsDir, "dotnetcloud-sync-tray.desktop");
            Assert.IsTrue(File.Exists(launcherPath));

            var launcherContents = await File.ReadAllTextAsync(launcherPath);
            StringAssert.Contains(launcherContents, "Name=DotNetCloud Sync Client");
            var escapedLauncherExecPath = trayExecutablePath
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
            StringAssert.Contains(launcherContents, $"Exec=\"{escapedLauncherExecPath}\"");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static (SettingsViewModel vm, Mock<IIpcClient> ipcMock, Mock<IOAuth2Service> oauth2Mock, Mock<INotificationService> notifMock)
        BuildVm()
    {
        var ipcMock = new Mock<IIpcClient>();
        ipcMock.SetupGet(i => i.IsConnected).Returns(false);
        ipcMock.SetupAdd(i => i.SyncProgressReceived += It.IsAny<EventHandler<SyncProgressEventData>>());
        ipcMock.SetupAdd(i => i.SyncCompleteReceived += It.IsAny<EventHandler<SyncCompleteEventData>>());
        ipcMock.SetupAdd(i => i.SyncErrorReceived += It.IsAny<EventHandler<SyncErrorEventData>>());
        ipcMock.SetupAdd(i => i.ConflictDetected += It.IsAny<EventHandler<SyncConflictEventData>>());
        ipcMock.SetupAdd(i => i.ConnectionStateChanged += It.IsAny<EventHandler<bool>>());

        var chatMock = new Mock<IChatSignalRClient>();
        chatMock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var notifMock = new Mock<INotificationService>();
        var trayVm = new TrayViewModel(ipcMock.Object, chatMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);

        var oauth2Mock = new Mock<IOAuth2Service>();
        var startupManager = new Mock<IDesktopStartupManager>();
        startupManager.Setup(m => m.TryApplyStartOnLogin(It.IsAny<bool>())).Returns(true);

        var settingsVm = new SettingsViewModel(
            trayVm, ipcMock.Object, oauth2Mock.Object,
            new Mock<ISyncIgnoreParser>().Object,
            new Mock<ISelectiveSyncConfig>().Object,
            startupManager.Object,
            NullLogger<SettingsViewModel>.Instance);

        return (settingsVm, ipcMock, oauth2Mock, notifMock);
    }

    /// <summary>
    /// Creates a minimal JWT (header.payload.signature) with the given <c>sub</c> claim
    /// so that <see cref="SettingsViewModel"/> can extract a user ID from it.
    /// </summary>
    private static string BuildFakeJwt(Guid userId)
    {
        var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"alg\":\"none\"}")).TrimEnd('=');
        var payload = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{{\"sub\":\"{userId}\"}}")).TrimEnd('=');
        return $"{header}.{payload}.fake-signature";
    }
}
