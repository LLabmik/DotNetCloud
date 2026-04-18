using DotNetCloud.Client.Android.Auth;
using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Android.ViewModels;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Client.Android.Tests.ViewModels;

[TestClass]
public sealed class SettingsViewModelTests
{
    private Mock<IServerConnectionStore> _serverStore = null!;
    private Mock<ISecureTokenStore> _tokenStore = null!;
    private Mock<IMediaAutoUploadService> _mediaUploadService = null!;
    private Mock<IBatteryOptimizationService> _batteryService = null!;
    private Mock<IAppPreferences> _preferences = null!;
    private Mock<IAndroidUpdateService> _updateService = null!;
    private Mock<ILogger<SettingsViewModel>> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _serverStore = new Mock<IServerConnectionStore>();
        _tokenStore = new Mock<ISecureTokenStore>();
        _mediaUploadService = new Mock<IMediaAutoUploadService>();
        _batteryService = new Mock<IBatteryOptimizationService>();
        _preferences = new Mock<IAppPreferences>();
        _updateService = new Mock<IAndroidUpdateService>();
        _logger = new Mock<ILogger<SettingsViewModel>>();
    }

    [TestMethod]
    public void Constructor_LoadsServerInfo_FromServerStore()
    {
        // Arrange
        var connection = new ServerConnection(
            "https://example.com:15443",
            "My Server",
            "test@example.com");
        _serverStore.Setup(x => x.GetActive()).Returns(connection);

        // Act
        var vm = CreateViewModel();

        // Assert
        Assert.AreEqual("My Server", vm.ServerDisplayName);
        Assert.AreEqual("test@example.com", vm.AccountEmail);
        Assert.AreEqual("https://example.com:15443", vm.ServerBaseUrl);
    }

    [TestMethod]
    public void Constructor_SetsEmptyStrings_WhenNoActiveServer()
    {
        // Arrange
        _serverStore.Setup(x => x.GetActive()).Returns((ServerConnection?)null);

        // Act
        var vm = CreateViewModel();

        // Assert
        Assert.AreEqual(string.Empty, vm.ServerDisplayName);
        Assert.AreEqual(string.Empty, vm.AccountEmail);
        Assert.AreEqual(string.Empty, vm.ServerBaseUrl);
    }

    [TestMethod]
    public void Constructor_LoadsSyncPreferences_FromAppPreferences()
    {
        // Arrange
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefEnabled, false)).Returns(true);
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefWifiOnly, true)).Returns(false);
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefOrganizeByDate, true)).Returns(false);
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefUploadFolderName, SettingsViewModel.DefaultUploadFolderName))
            .Returns("MyFolder");

        // Act
        var vm = CreateViewModel();

        // Assert
        Assert.IsTrue(vm.AutoUploadEnabled);
        Assert.IsFalse(vm.WifiOnlyEnabled);
        Assert.IsFalse(vm.OrganizeByDate);
        Assert.AreEqual("MyFolder", vm.UploadFolderName);
    }

    [TestMethod]
    public void Constructor_LoadsDefaultPreferences_WhenNotSet()
    {
        // Arrange
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefEnabled, false)).Returns(false);
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefWifiOnly, true)).Returns(true);
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefOrganizeByDate, true)).Returns(true);
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefUploadFolderName, SettingsViewModel.DefaultUploadFolderName))
            .Returns(SettingsViewModel.DefaultUploadFolderName);

        // Act
        var vm = CreateViewModel();

        // Assert
        Assert.IsFalse(vm.AutoUploadEnabled);
        Assert.IsTrue(vm.WifiOnlyEnabled);
        Assert.IsTrue(vm.OrganizeByDate);
        Assert.AreEqual("InstantUpload", vm.UploadFolderName);
    }

    [TestMethod]
    public void RefreshBatteryStatus_SetsBatteryOptimized_WhenRestricted()
    {
        // Arrange
        _batteryService.Setup(x => x.IsIgnoringBatteryOptimizations()).Returns(false);
        var vm = CreateViewModel();

        // Act
        vm.RefreshBatteryStatus();

        // Assert
        Assert.IsTrue(vm.IsBatteryOptimized);
        Assert.AreEqual("Restricted — tap to fix", vm.BatteryStatusText);
        Assert.AreEqual(Color.FromArgb("#F59E0B"), vm.BatteryStatusColor);
    }

    [TestMethod]
    public void RefreshBatteryStatus_SetsUnrestricted_WhenExempt()
    {
        // Arrange
        _batteryService.Setup(x => x.IsIgnoringBatteryOptimizations()).Returns(true);
        var vm = CreateViewModel();

        // Act
        vm.RefreshBatteryStatus();

        // Assert
        Assert.IsFalse(vm.IsBatteryOptimized);
        Assert.AreEqual("Unrestricted", vm.BatteryStatusText);
        Assert.AreEqual(Color.FromArgb("#22C55E"), vm.BatteryStatusColor);
    }

    [TestMethod]
    public void AutoUploadEnabled_PersistsToPreferences_AndStartsService()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.AutoUploadEnabled = true;

        // Assert
        _preferences.Verify(x => x.Set(SettingsViewModel.PrefEnabled, true), Times.Once);
        _mediaUploadService.Verify(x => x.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public void AutoUploadEnabled_False_PersistsToPreferences_AndStopsService()
    {
        // Arrange
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefEnabled, false)).Returns(true);
        var vm = CreateViewModel();

        // Act
        vm.AutoUploadEnabled = false;

        // Assert
        _preferences.Verify(x => x.Set(SettingsViewModel.PrefEnabled, false), Times.Once);
        _mediaUploadService.Verify(x => x.StopAsync(), Times.Once);
    }

    [TestMethod]
    public void WifiOnlyEnabled_PersistsToPreferences()
    {
        // Arrange
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefWifiOnly, true)).Returns(true);
        var vm = CreateViewModel();

        // Act
        vm.WifiOnlyEnabled = false;

        // Assert
        _preferences.Verify(x => x.Set(SettingsViewModel.PrefWifiOnly, false), Times.Once);
    }

    [TestMethod]
    public void OrganizeByDate_PersistsToPreferences()
    {
        // Arrange
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefOrganizeByDate, true)).Returns(true);
        var vm = CreateViewModel();

        // Act
        vm.OrganizeByDate = false;

        // Assert
        _preferences.Verify(x => x.Set(SettingsViewModel.PrefOrganizeByDate, false), Times.Once);
    }

    [TestMethod]
    public void UploadFolderName_PersistsToPreferences_WhenNotEmpty()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.UploadFolderName = "  MyNewFolder  ";

        // Assert
        _preferences.Verify(x => x.Set(SettingsViewModel.PrefUploadFolderName, "MyNewFolder"), Times.Once);
    }

    [TestMethod]
    public void UploadFolderName_DoesNotPersist_WhenEmptyOrWhitespace()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.UploadFolderName = "   ";

        // Assert
        _preferences.Verify(x => x.Set(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RequestBatteryExemptionCommand_RequestsExemption_AndRefreshesStatus()
    {
        // Arrange
        _batteryService.Setup(x => x.IsIgnoringBatteryOptimizations()).Returns(false);
        var vm = CreateViewModel();

        // Act
        await vm.RequestBatteryExemptionCommand.ExecuteAsync(null);

        // Assert
        _batteryService.Verify(x => x.RequestExemptionAsync(), Times.Once);
        // RefreshBatteryStatus is called after delay
        _batteryService.Verify(x => x.IsIgnoringBatteryOptimizations(), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task SyncNowCommand_CallsScanAndUpload_WhenEnabled()
    {
        // Arrange
        _preferences.Setup(x => x.Get(SettingsViewModel.PrefEnabled, false)).Returns(true);
        var vm = CreateViewModel();

        // Act
        await vm.SyncNowCommand.ExecuteAsync(CancellationToken.None);

        // Assert
        _mediaUploadService.Verify(x => x.ScanAndUploadNowAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task LogOutCommand_DeletesTokens_RemovesServer_RaisesEvent()
    {
        // Arrange
        var connection = new ServerConnection(
            "https://example.com:15443",
            "My Server",
            "test@example.com");
        _serverStore.Setup(x => x.GetActive()).Returns(connection);
        var vm = CreateViewModel();
        var loggedOutRaised = false;
        vm.LoggedOut += (_, _) => loggedOutRaised = true;

        // Act
        await vm.LogOutCommand.ExecuteAsync(CancellationToken.None);

        // Assert
        _tokenStore.Verify(x => x.DeleteTokensAsync("https://example.com:15443", It.IsAny<CancellationToken>()), Times.Once);
        _serverStore.Verify(x => x.Remove("https://example.com:15443"), Times.Once);
        Assert.IsTrue(loggedOutRaised);
    }

    [TestMethod]
    public async Task LogOutCommand_SetsIsBusy_DuringExecution()
    {
        // Arrange
        var connection = new ServerConnection(
            "https://example.com:15443",
            "My Server",
            "test@example.com");
        _serverStore.Setup(x => x.GetActive()).Returns(connection);

        var tcs = new TaskCompletionSource<bool>();
        _tokenStore.Setup(x => x.DeleteTokensAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () => { await tcs.Task; });

        var vm = CreateViewModel();
        var busyStates = new List<bool>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.IsBusy))
                busyStates.Add(vm.IsBusy);
        };

        // Act
        var logoutTask = vm.LogOutCommand.ExecuteAsync(CancellationToken.None);
        await Task.Delay(50); // Let IsBusy=true propagate
        tcs.SetResult(true);
        await logoutTask;

        // Assert
        Assert.IsTrue(busyStates.Contains(true), "IsBusy should have been set to true");
        Assert.IsFalse(vm.IsBusy, "IsBusy should be false after completion");
    }

    [TestMethod]
    public async Task LogOutCommand_DoesNothing_WhenServerBaseUrlEmpty()
    {
        // Arrange
        _serverStore.Setup(x => x.GetActive()).Returns((ServerConnection?)null);
        var vm = CreateViewModel();

        // Act
        await vm.LogOutCommand.ExecuteAsync(CancellationToken.None);

        // Assert
        _tokenStore.Verify(x => x.DeleteTokensAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _serverStore.Verify(x => x.Remove(It.IsAny<string>()), Times.Never);
    }

    private SettingsViewModel CreateViewModel()
    {
        return new SettingsViewModel(
            _serverStore.Object,
            _tokenStore.Object,
            _mediaUploadService.Object,
            _batteryService.Object,
            _preferences.Object,
            _updateService.Object,
            _logger.Object);
    }
}
