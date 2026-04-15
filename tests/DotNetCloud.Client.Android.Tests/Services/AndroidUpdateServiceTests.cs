using DotNetCloud.Client.Android.Services;
using DotNetCloud.Client.Core.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Client.Android.Tests.Services;

[TestClass]
public sealed class AndroidUpdateServiceTests
{
    private Mock<IClientUpdateService> _clientUpdateService = null!;
    private Mock<IAppPreferences> _preferences = null!;
    private Mock<ILogger<AndroidUpdateService>> _logger = null!;
    private AndroidUpdateService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _clientUpdateService = new Mock<IClientUpdateService>();
        _preferences = new Mock<IAppPreferences>();
        _logger = new Mock<ILogger<AndroidUpdateService>>();

        // Default: no previous check today, no dismissed version.
        _preferences.Setup(p => p.Get(AndroidUpdateService.PrefLastCheckDate, string.Empty))
            .Returns(string.Empty);
        _preferences.Setup(p => p.Get(AndroidUpdateService.PrefDismissedVersion, string.Empty))
            .Returns(string.Empty);

        _sut = new AndroidUpdateService(_clientUpdateService.Object, _preferences.Object, _logger.Object);
    }

    [TestMethod]
    public async Task CheckOnLaunchAsync_ReturnsResult_WhenUpdateAvailable()
    {
        // Arrange
        var expected = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = "0.1.7",
            LatestVersion = "0.2.0",
            ReleaseUrl = "https://github.com/LLabmik/DotNetCloud/releases/tag/v0.2.0",
            ReleaseNotes = "New features!",
        };
        _clientUpdateService.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.CheckOnLaunchAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsUpdateAvailable);
        Assert.AreEqual("0.2.0", result.LatestVersion);
    }

    [TestMethod]
    public async Task CheckOnLaunchAsync_ReturnsNull_WhenNoUpdateAvailable()
    {
        // Arrange
        var noUpdate = new UpdateCheckResult
        {
            IsUpdateAvailable = false,
            CurrentVersion = "0.2.0",
            LatestVersion = "0.2.0",
        };
        _clientUpdateService.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(noUpdate);

        // Act
        var result = await _sut.CheckOnLaunchAsync();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CheckOnLaunchAsync_SkipsCheck_WhenAlreadyCheckedToday()
    {
        // Arrange
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        _preferences.Setup(p => p.Get(AndroidUpdateService.PrefLastCheckDate, string.Empty))
            .Returns(today);

        // Act
        var result = await _sut.CheckOnLaunchAsync();

        // Assert
        Assert.IsNull(result);
        _clientUpdateService.Verify(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task CheckOnLaunchAsync_RecordsCheckDate_AfterSuccessfulCheck()
    {
        // Arrange
        var noUpdate = new UpdateCheckResult
        {
            IsUpdateAvailable = false,
            CurrentVersion = "0.2.0",
            LatestVersion = "0.2.0",
        };
        _clientUpdateService.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(noUpdate);

        // Act
        await _sut.CheckOnLaunchAsync();

        // Assert
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        _preferences.Verify(p => p.Set(AndroidUpdateService.PrefLastCheckDate, today), Times.Once);
    }

    [TestMethod]
    public async Task CheckOnLaunchAsync_ReturnsNull_WhenVersionDismissed()
    {
        // Arrange
        _preferences.Setup(p => p.Get(AndroidUpdateService.PrefDismissedVersion, string.Empty))
            .Returns("0.2.0");

        var update = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = "0.1.7",
            LatestVersion = "0.2.0",
        };
        _clientUpdateService.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(update);

        // Act
        var result = await _sut.CheckOnLaunchAsync();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CheckOnLaunchAsync_ReturnsResult_WhenDifferentVersionDismissed()
    {
        // Arrange — dismissed 0.1.9 but 0.2.0 is now available.
        _preferences.Setup(p => p.Get(AndroidUpdateService.PrefDismissedVersion, string.Empty))
            .Returns("0.1.9");

        var update = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = "0.1.7",
            LatestVersion = "0.2.0",
            ReleaseUrl = "https://example.com",
        };
        _clientUpdateService.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(update);

        // Act
        var result = await _sut.CheckOnLaunchAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("0.2.0", result.LatestVersion);
    }

    [TestMethod]
    public async Task CheckOnLaunchAsync_ReturnsNull_WhenCheckThrows()
    {
        // Arrange
        _clientUpdateService.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _sut.CheckOnLaunchAsync();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task CheckOnLaunchAsync_PropagatesCancellation()
    {
        // Arrange
        _clientUpdateService.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.CheckOnLaunchAsync());
    }

    [TestMethod]
    public void DismissVersion_StoresVersionInPreferences()
    {
        // Act
        _sut.DismissVersion("0.2.0");

        // Assert
        _preferences.Verify(p => p.Set(AndroidUpdateService.PrefDismissedVersion, "0.2.0"), Times.Once);
    }

    [TestMethod]
    public void DismissVersion_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => _sut.DismissVersion(""));
    }

    [TestMethod]
    public void DismissVersion_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => _sut.DismissVersion(null!));
    }
}
