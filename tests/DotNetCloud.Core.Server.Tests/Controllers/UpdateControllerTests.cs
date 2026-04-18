using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Server.Controllers;
using DotNetCloud.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DotNetCloud.Core.Server.Tests.Controllers;

[TestClass]
public class UpdateControllerTests
{
    private Mock<IUpdateService> _updateServiceMock = null!;
    private Mock<ILogger<UpdateController>> _loggerMock = null!;
    private UpdateController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _updateServiceMock = new Mock<IUpdateService>();
        _loggerMock = new Mock<ILogger<UpdateController>>();
        _controller = new UpdateController(_updateServiceMock.Object, _loggerMock.Object);
    }

    // -----------------------------------------------------------------------
    // CheckForUpdateAsync
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task CheckForUpdateAsync_WhenUpdateAvailable_ReturnsOkWithData()
    {
        // Arrange
        var expected = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = "0.1.7-alpha",
            LatestVersion = "0.2.0",
            ReleaseUrl = "https://github.com/LLabmik/DotNetCloud/releases/tag/v0.2.0",
            ReleaseNotes = "## What's New\n- Feature A\n- Fix B",
            PublishedAt = DateTimeOffset.UtcNow.AddDays(-1),
            Assets = [
                new ReleaseAsset
                {
                    Name = "dotnetcloud-0.2.0-linux-x64.tar.gz",
                    DownloadUrl = "https://github.com/LLabmik/DotNetCloud/releases/download/v0.2.0/dotnetcloud-0.2.0-linux-x64.tar.gz",
                    Size = 50_000_000,
                    ContentType = "application/gzip",
                    Platform = "linux-x64",
                }
            ],
        };

        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _controller.CheckForUpdateAsync();

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(result);
        _updateServiceMock.Verify(s => s.CheckForUpdateAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_WithExplicitVersion_PassesVersionToService()
    {
        // Arrange
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync("0.1.5", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult
            {
                IsUpdateAvailable = true,
                CurrentVersion = "0.1.5",
                LatestVersion = "0.2.0",
            });

        // Act
        var result = await _controller.CheckForUpdateAsync("0.1.5");

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(result);
        _updateServiceMock.Verify(s => s.CheckForUpdateAsync("0.1.5", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task CheckForUpdateAsync_WhenUpToDate_ReturnsOkWithNoUpdate()
    {
        // Arrange
        _updateServiceMock
            .Setup(s => s.CheckForUpdateAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = "0.2.0",
                LatestVersion = "0.2.0",
            });

        // Act
        var result = await _controller.CheckForUpdateAsync();

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    // -----------------------------------------------------------------------
    // GetRecentReleasesAsync
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task GetRecentReleasesAsync_ReturnsOkWithReleases()
    {
        // Arrange
        var releases = new List<ReleaseInfo>
        {
            new() { Version = "0.2.0", TagName = "v0.2.0", IsPreRelease = false },
            new() { Version = "0.1.7", TagName = "v0.1.7", IsPreRelease = true },
        };

        _updateServiceMock
            .Setup(s => s.GetRecentReleasesAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(releases.AsReadOnly());

        // Act
        var result = await _controller.GetRecentReleasesAsync();

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(result);
        _updateServiceMock.Verify(s => s.GetRecentReleasesAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetRecentReleasesAsync_ClampsCountTo20()
    {
        // Arrange
        _updateServiceMock
            .Setup(s => s.GetRecentReleasesAsync(20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReleaseInfo>().AsReadOnly());

        // Act
        var result = await _controller.GetRecentReleasesAsync(count: 100);

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(result);
        _updateServiceMock.Verify(s => s.GetRecentReleasesAsync(20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetRecentReleasesAsync_ClampsCountToMinimum1()
    {
        // Arrange
        _updateServiceMock
            .Setup(s => s.GetRecentReleasesAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReleaseInfo>().AsReadOnly());

        // Act
        var result = await _controller.GetRecentReleasesAsync(count: 0);

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(result);
        _updateServiceMock.Verify(s => s.GetRecentReleasesAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // GetLatestReleaseAsync
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task GetLatestReleaseAsync_WhenReleaseExists_ReturnsOk()
    {
        // Arrange
        var release = new ReleaseInfo
        {
            Version = "0.2.0",
            TagName = "v0.2.0",
            ReleaseNotes = "New release",
            PublishedAt = DateTimeOffset.UtcNow,
            IsPreRelease = false,
            ReleaseUrl = "https://github.com/LLabmik/DotNetCloud/releases/tag/v0.2.0",
        };

        _updateServiceMock
            .Setup(s => s.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(release);

        // Act
        var result = await _controller.GetLatestReleaseAsync();

        // Assert
        Assert.IsInstanceOfType<OkObjectResult>(result);
    }

    [TestMethod]
    public async Task GetLatestReleaseAsync_WhenNoReleases_ReturnsNotFound()
    {
        // Arrange
        _updateServiceMock
            .Setup(s => s.GetLatestReleaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReleaseInfo?)null);

        // Act
        var result = await _controller.GetLatestReleaseAsync();

        // Assert
        Assert.IsInstanceOfType<NotFoundObjectResult>(result);
    }
}
