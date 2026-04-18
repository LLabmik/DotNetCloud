using DotNetCloud.Client.Core.Services;
using DotNetCloud.Client.SyncTray.Services;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Client.SyncTray.Tests.Services;

[TestClass]
public sealed class UpdateCheckBackgroundServiceTests
{
    [TestMethod]
    public async Task CheckAsync_UpdateAvailable_RaisesEvent()
    {
        var mockUpdate = new Mock<IClientUpdateService>();
        mockUpdate
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult
            {
                IsUpdateAvailable = true,
                CurrentVersion = "0.1.7-alpha",
                LatestVersion = "0.2.0",
                Assets = [],
            });

        var svc = new UpdateCheckBackgroundService(mockUpdate.Object, NullLogger<UpdateCheckBackgroundService>.Instance);

        UpdateCheckResult? eventResult = null;
        svc.UpdateAvailable += (_, r) => eventResult = r;

        await svc.CheckAsync();

        Assert.IsNotNull(eventResult);
        Assert.IsTrue(eventResult!.IsUpdateAvailable);
        Assert.AreEqual("0.2.0", eventResult.LatestVersion);
    }

    [TestMethod]
    public async Task CheckAsync_NoUpdate_DoesNotRaiseEvent()
    {
        var mockUpdate = new Mock<IClientUpdateService>();
        mockUpdate
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = "0.1.7-alpha",
                LatestVersion = "0.1.7-alpha",
                Assets = [],
            });

        var svc = new UpdateCheckBackgroundService(mockUpdate.Object, NullLogger<UpdateCheckBackgroundService>.Instance);

        bool eventFired = false;
        svc.UpdateAvailable += (_, _) => eventFired = true;

        await svc.CheckAsync();

        Assert.IsFalse(eventFired);
    }

    [TestMethod]
    public async Task CheckAsync_ServiceThrows_ReturnsGracefully()
    {
        var mockUpdate = new Mock<IClientUpdateService>();
        mockUpdate
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var svc = new UpdateCheckBackgroundService(mockUpdate.Object, NullLogger<UpdateCheckBackgroundService>.Instance);

        var result = await svc.CheckAsync();

        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsUpdateAvailable);
    }

    [TestMethod]
    public async Task CheckAsync_StoresLatestResult()
    {
        var expected = new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = "0.1.7",
            LatestVersion = "1.0.0",
            Assets = [],
        };

        var mockUpdate = new Mock<IClientUpdateService>();
        mockUpdate
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var svc = new UpdateCheckBackgroundService(mockUpdate.Object, NullLogger<UpdateCheckBackgroundService>.Instance);
        Assert.IsNull(svc.LatestCheckResult);

        await svc.CheckAsync();

        Assert.IsNotNull(svc.LatestCheckResult);
        Assert.AreEqual("1.0.0", svc.LatestCheckResult!.LatestVersion);
        Assert.IsNotNull(svc.LastCheckedAtUtc);
    }

    [TestMethod]
    public void StartAndStop_DoesNotThrow()
    {
        var mockUpdate = new Mock<IClientUpdateService>();
        mockUpdate
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult
            {
                IsUpdateAvailable = false,
                CurrentVersion = "0.1.7",
                LatestVersion = "0.1.7",
                Assets = [],
            });

        var svc = new UpdateCheckBackgroundService(mockUpdate.Object, NullLogger<UpdateCheckBackgroundService>.Instance);

        svc.Start();
        svc.Stop();
        svc.Dispose();
    }

    [TestMethod]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        var mockUpdate = new Mock<IClientUpdateService>();
        var svc = new UpdateCheckBackgroundService(mockUpdate.Object, NullLogger<UpdateCheckBackgroundService>.Instance);

        svc.Dispose();
        svc.Dispose(); // Should not throw.
    }

    [TestMethod]
    public void IsEnabled_DefaultsToTrue()
    {
        var mockUpdate = new Mock<IClientUpdateService>();
        var svc = new UpdateCheckBackgroundService(mockUpdate.Object, NullLogger<UpdateCheckBackgroundService>.Instance);

        Assert.IsTrue(svc.IsEnabled);
    }

    [TestMethod]
    public void CheckInterval_DefaultIs24Hours()
    {
        var mockUpdate = new Mock<IClientUpdateService>();
        var svc = new UpdateCheckBackgroundService(mockUpdate.Object, NullLogger<UpdateCheckBackgroundService>.Instance);

        Assert.AreEqual(TimeSpan.FromHours(24), svc.CheckInterval);
    }
}
