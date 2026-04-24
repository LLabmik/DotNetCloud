using DotNetCloud.Core.DTOs;
using DotNetCloud.Modules.Music.Services;

namespace DotNetCloud.Modules.Music.Tests;

[TestClass]
public class ScanProgressStateTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [TestMethod]
    public void IsScanning_DefaultFalse()
    {
        var state = new ScanProgressState();
        Assert.IsFalse(state.IsScanning(UserId));
    }

    [TestMethod]
    public void StartScan_SetsIsScanningTrue()
    {
        var state = new ScanProgressState();
        using var _ = state.StartScan(UserId);
        Assert.IsTrue(state.IsScanning(UserId));
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

        state.UpdateProgress(UserId, progress);

        var current = state.GetCurrentProgress(UserId);
        Assert.IsNotNull(current);
        Assert.AreEqual("Extracting metadata", current.Phase);
        Assert.AreEqual("track1.mp3", current.CurrentFile);
        Assert.AreEqual(5, current.FilesProcessed);
        Assert.AreEqual(20, current.TotalFiles);
        Assert.AreEqual(25, current.PercentComplete);
    }

    [TestMethod]
    public void UpdateProgress_FiresOnProgressChanged()
    {
        var state = new ScanProgressState();
        var fired = false;
        state.OnProgressChanged += () => fired = true;

        state.UpdateProgress(UserId, new LibraryScanProgress
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
        using var _ = state.StartScan(UserId);
        Assert.IsTrue(state.IsScanning(UserId));

        state.CompleteScan(UserId);
        Assert.IsFalse(state.IsScanning(UserId));
    }

    [TestMethod]
    public void CompleteScan_FiresOnProgressChanged()
    {
        var state = new ScanProgressState();
        using var _ = state.StartScan(UserId);

        var fired = false;
        state.OnProgressChanged += () => fired = true;

        state.CompleteScan(UserId);
        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void MultipleSubscribers_AllNotified()
    {
        var state = new ScanProgressState();
        var count = 0;
        state.OnProgressChanged += () => count++;
        state.OnProgressChanged += () => count++;

        using var _ = state.StartScan(UserId);

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void UpdateProgress_NullListener_DoesNotThrow()
    {
        var state = new ScanProgressState();

        // No subscribers — should not throw
        state.UpdateProgress(UserId, new LibraryScanProgress
        {
            Phase = "Test",
            ElapsedTime = TimeSpan.Zero
        });

        using var _ = state.StartScan(UserId);
        state.CompleteScan(UserId);
    }

    [TestMethod]
    public void Progress_IsIsolatedPerUser()
    {
        var state = new ScanProgressState();

        state.UpdateProgress(UserId, new LibraryScanProgress
        {
            Phase = "User one",
            FilesProcessed = 1,
            TotalFiles = 2,
            ElapsedTime = TimeSpan.FromSeconds(1)
        });

        state.UpdateProgress(OtherUserId, new LibraryScanProgress
        {
            Phase = "User two",
            FilesProcessed = 2,
            TotalFiles = 4,
            ElapsedTime = TimeSpan.FromSeconds(2)
        });

        Assert.AreEqual("User one", state.GetCurrentProgress(UserId)?.Phase);
        Assert.AreEqual("User two", state.GetCurrentProgress(OtherUserId)?.Phase);
    }
}
