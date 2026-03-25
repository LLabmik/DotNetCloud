using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.Sync;
using DotNetCloud.Client.Core.SyncIgnore;
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
    public void AddAccountServerUrl_DefaultsToEmptyString()
    {
        var (vm, _, _, _) = BuildVm();

        Assert.AreEqual(string.Empty, vm.AddAccountServerUrl);
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
        var (vm, syncMock, oauth2Mock, _) = BuildVm();

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

        syncMock
            .Setup(i => i.AddContextAsync(It.IsAny<AddAccountRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncContextRegistration
            {
                Id = Guid.NewGuid(),
                ServerBaseUrl = "https://cloud.example.com",
                UserId = Guid.NewGuid(),
                LocalFolderPath = "/tmp/sync",
                DisplayName = "test",
                AccountKey = "test-key",
                OsUserName = "testuser",
                DataDirectory = "/tmp/data",
            });

        syncMock
            .Setup(i => i.GetContextsAsync())
            .ReturnsAsync([]);

        await vm.AddAccountAsync("https://cloud.example.com", "/tmp/sync");

        Assert.AreEqual(string.Empty, vm.AddAccountError);
        oauth2Mock.Verify(
            o => o.AuthorizeAsync("https://cloud.example.com", It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        syncMock.Verify(i => i.AddContextAsync(It.IsAny<AddAccountRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task AddAccountAsync_ExistingAccount_SetsErrorAndSkipsAddFlow()
    {
        var (vm, syncMock, oauth2Mock, _) = BuildVm();

        var existingContextId = Guid.NewGuid();
        syncMock
            .Setup(i => i.GetContextsAsync())
            .ReturnsAsync([
                new SyncContextRegistration
                {
                    Id = existingContextId,
                    DisplayName = "test@example.com @ cloud.example.com",
                    ServerBaseUrl = "https://cloud.example.com",
                    LocalFolderPath = "/tmp/existing-sync",
                    UserId = Guid.NewGuid(),
                    AccountKey = "existing-key",
                    OsUserName = "testuser",
                    DataDirectory = "/tmp/data",
                }
            ]);

        syncMock
            .Setup(i => i.GetStatusAsync(existingContextId))
            .ReturnsAsync(new SyncStatus { State = SyncState.Idle });

        await vm.TrayVm.RefreshAccountsAsync();
        Assert.IsTrue(vm.HasAccount);

        await vm.AddAccountAsync("https://another.example.com", "/tmp/new-sync");

        StringAssert.Contains(vm.AddAccountError, "Only one account");
        oauth2Mock.Verify(
            o => o.AuthorizeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        syncMock.Verify(
            i => i.AddContextAsync(It.IsAny<AddAccountRequest>(), It.IsAny<CancellationToken>()),
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
        var (vm, syncMock, _, _) = BuildVm();
        var contextId = Guid.NewGuid();

        syncMock
            .Setup(i => i.RemoveContextAsync(contextId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await vm.RemoveAccountAsync(contextId);

        syncMock.Verify(i => i.RemoveContextAsync(contextId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task IsMuteChatNotifications_PersistsAndLoadsFromLocalSettingsJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnetcloud-sync-tray-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var settingsPath = Path.Combine(tempDir, "sync-tray-settings.json");

        try
        {
            var syncMock = new Mock<ISyncContextManager>();
            syncMock.Setup(s => s.GetContextsAsync()).ReturnsAsync(new List<SyncContextRegistration>());

            var chatMock = new Mock<IChatSignalRClient>();
            chatMock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var notifMock = new Mock<INotificationService>();
            var trayVm = new TrayViewModel(syncMock.Object, chatMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);
            var oauth2Mock = new Mock<IOAuth2Service>();
            var startupManager = new Mock<IDesktopStartupManager>();
            startupManager.Setup(m => m.TryApplyStartOnLogin(It.IsAny<bool>())).Returns(true);

            var vm1 = new SettingsViewModel(
                trayVm,
                syncMock.Object,
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
                syncMock.Object,
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
            var syncMock = new Mock<ISyncContextManager>();
            syncMock.Setup(s => s.GetContextsAsync()).ReturnsAsync(new List<SyncContextRegistration>());

            var chatMock = new Mock<IChatSignalRClient>();
            chatMock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var notifMock = new Mock<INotificationService>();
            var trayVm = new TrayViewModel(syncMock.Object, chatMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);
            var startupManager = new DesktopStartupManager(
                NullLogger<DesktopStartupManager>.Instance,
                trayExecutablePathProvider: () => trayExecutablePath,
                autostartDirectory: autostartDir,
                isLinux: () => true);

            var vm1 = new SettingsViewModel(
                trayVm,
                syncMock.Object,
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
            var normalizedDesktopFileContents = desktopFileContents.Replace("\\\\", "\\", StringComparison.Ordinal);
            StringAssert.Contains(normalizedDesktopFileContents, $"Exec=\"{trayExecutablePath}\"");

            var vm2 = new SettingsViewModel(
                trayVm,
                syncMock.Object,
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
            var syncMock = new Mock<ISyncContextManager>();
            syncMock.Setup(s => s.GetContextsAsync()).ReturnsAsync(new List<SyncContextRegistration>());

            var chatMock = new Mock<IChatSignalRClient>();
            chatMock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var notifMock = new Mock<INotificationService>();
            var trayVm = new TrayViewModel(syncMock.Object, chatMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);
            var startupManager = new DesktopStartupManager(
                NullLogger<DesktopStartupManager>.Instance,
                trayExecutablePathProvider: () => trayExecutablePath,
                autostartDirectory: autostartDir,
                isLinux: () => true);

            var vm1 = new SettingsViewModel(
                trayVm,
                syncMock.Object,
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
                syncMock.Object,
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
            var normalizedLauncherContents = launcherContents.Replace("\\\\", "\\", StringComparison.Ordinal);
            StringAssert.Contains(launcherContents, "Name=DotNetCloud Sync Client");
            StringAssert.Contains(normalizedLauncherContents, $"Exec=\"{trayExecutablePath}\"");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static (SettingsViewModel vm, Mock<ISyncContextManager> syncMock, Mock<IOAuth2Service> oauth2Mock, Mock<INotificationService> notifMock)
        BuildVm()
    {
        var syncMock = new Mock<ISyncContextManager>();
        syncMock.Setup(s => s.GetContextsAsync()).ReturnsAsync(new List<SyncContextRegistration>());

        var chatMock = new Mock<IChatSignalRClient>();
        chatMock.Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var notifMock = new Mock<INotificationService>();
        var trayVm = new TrayViewModel(syncMock.Object, chatMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);

        var oauth2Mock = new Mock<IOAuth2Service>();
        var startupManager = new Mock<IDesktopStartupManager>();
        startupManager.Setup(m => m.TryApplyStartOnLogin(It.IsAny<bool>())).Returns(true);

        var settingsVm = new SettingsViewModel(
            trayVm, syncMock.Object, oauth2Mock.Object,
            new Mock<ISyncIgnoreParser>().Object,
            new Mock<ISelectiveSyncConfig>().Object,
            startupManager.Object,
            NullLogger<SettingsViewModel>.Instance);

        return (settingsVm, syncMock, oauth2Mock, notifMock);
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
