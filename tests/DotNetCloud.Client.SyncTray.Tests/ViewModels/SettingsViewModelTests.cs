using DotNetCloud.Client.Core.Auth;
using DotNetCloud.Client.Core.SelectiveSync;
using DotNetCloud.Client.Core.SyncIgnore;
using DotNetCloud.Client.SyncTray.Ipc;
using DotNetCloud.Client.SyncTray.Notifications;
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

        var notifMock = new Mock<INotificationService>();
        var trayVm = new TrayViewModel(ipcMock.Object, notifMock.Object, NullLogger<TrayViewModel>.Instance);

        var oauth2Mock = new Mock<IOAuth2Service>();

        var settingsVm = new SettingsViewModel(
            trayVm, ipcMock.Object, oauth2Mock.Object,
            new Mock<ISyncIgnoreParser>().Object,
            new Mock<ISelectiveSyncConfig>().Object,
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
