using DotNetCloud.Client.Core.Services;
using DotNetCloud.Client.SyncTray.Services;
using DotNetCloud.Client.SyncTray.ViewModels;
using DotNetCloud.Core.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Client.SyncTray.Tests.ViewModels;

[TestClass]
public sealed class UpdateViewModelTests
{
    [TestMethod]
    public async Task Constructor_PrePopulatesFromLatestBackgroundCheck()
    {
        var mockUpdate = new Mock<IClientUpdateService>();
        mockUpdate
            .Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResult
            {
                IsUpdateAvailable = true,
                CurrentVersion = "0.4.0-alpha",
                LatestVersion = "0.5.0",
                Assets = [],
            });

        var background = new UpdateCheckBackgroundService(
            mockUpdate.Object,
            NullLogger<UpdateCheckBackgroundService>.Instance);
        await background.CheckAsync();

        var vm = new UpdateViewModel(
            mockUpdate.Object,
            background,
            NullLogger<UpdateViewModel>.Instance);

        Assert.AreEqual("0.4.0-alpha", vm.CurrentVersion);
        Assert.AreEqual("0.5.0", vm.LatestVersion);
        Assert.IsTrue(vm.IsUpdateAvailable);
    }

    [TestMethod]
    public void Constructor_WithDownloadedFilePath_SurfacesDownloadedState()
    {
        var mockUpdate = new Mock<IClientUpdateService>();
        var background = new UpdateCheckBackgroundService(
            mockUpdate.Object,
            NullLogger<UpdateCheckBackgroundService>.Instance);

        var tempFile = Path.Combine(
            Path.GetTempPath(),
            "DotNetCloud",
            "updates",
            "test-" + Guid.NewGuid().ToString("N") + ".tar.gz");
        Directory.CreateDirectory(Path.GetDirectoryName(tempFile)!);
        File.WriteAllText(tempFile, "fake-update");

        try
        {
            var vm = new UpdateViewModel(
                mockUpdate.Object,
                background,
                NullLogger<UpdateViewModel>.Instance,
                downloadedFilePath: tempFile,
                downloadedVersion: "0.5.0");

            Assert.IsTrue(vm.IsDownloadComplete);
            Assert.AreEqual(tempFile, vm.DownloadedFilePath);
            Assert.IsFalse(vm.CanDownload);
            Assert.IsFalse(string.IsNullOrWhiteSpace(vm.DownloadedSizeText));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
