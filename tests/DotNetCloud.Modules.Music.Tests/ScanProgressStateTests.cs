using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Services;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class ScanProgressStateTests
{
    [TestMethod]
    public void IsScanning_DefaultFalse()
    {
        var state = new ScanProgressState();
        Assert.IsFalse(state.IsScanning);
    }

    [TestMethod]
    public void StartScan_SetsIsScanningTrue()
    {
        var state = new ScanProgressState();
        state.StartScan();
        Assert.IsTrue(state.IsScanning);
    }

    [TestMethod]
    public void UpdateProgress_SetsCurrentProgress()
    {
        var state = new ScanProgressState();
        var progress = new LibraryScanProgress
        {
            Phase = "Extracting metadata",
            CurrentFile = "track1.mp3",
            FilesProcessed = 5,
            TotalFiles = 20,
            PercentComplete = 25,
            ElapsedTime = TimeSpan.FromSeconds(10)
        };

        state.UpdateProgress(progress);

        Assert.IsNotNull(state.CurrentProgress);
        Assert.AreEqual("Extracting metadata", state.CurrentProgress.Phase);
        Assert.AreEqual("track1.mp3", state.CurrentProgress.CurrentFile);
        Assert.AreEqual(5, state.CurrentProgress.FilesProcessed);
        Assert.AreEqual(20, state.CurrentProgress.TotalFiles);
        Assert.AreEqual(25, state.CurrentProgress.PercentComplete);
    }

    [TestMethod]
    public void UpdateProgress_FiresOnProgressChanged()
    {
        var state = new ScanProgressState();
        var fired = false;
        state.OnProgressChanged += () => fired = true;

        state.UpdateProgress(new LibraryScanProgress
        {
            Phase = "Test",
            ElapsedTime = TimeSpan.Zero
        });

        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void CompleteScan_SetsIsScanningFalse()
    {
        var state = new ScanProgressState();
        state.StartScan();
        Assert.IsTrue(state.IsScanning);

        state.CompleteScan();
        Assert.IsFalse(state.IsScanning);
    }

    [TestMethod]
    public void CompleteScan_FiresOnProgressChanged()
    {
        var state = new ScanProgressState();
        state.StartScan();

        var fired = false;
        state.OnProgressChanged += () => fired = true;

        state.CompleteScan();
        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void MultipleSubscribers_AllNotified()
    {
        var state = new ScanProgressState();
        var count = 0;
        state.OnProgressChanged += () => count++;
        state.OnProgressChanged += () => count++;

        state.StartScan();

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void UpdateProgress_NullListener_DoesNotThrow()
    {
        var state = new ScanProgressState();

        // No subscribers — should not throw
        state.UpdateProgress(new LibraryScanProgress
        {
            Phase = "Test",
            ElapsedTime = TimeSpan.Zero
        });

        state.StartScan();
        state.CompleteScan();
    }
}
