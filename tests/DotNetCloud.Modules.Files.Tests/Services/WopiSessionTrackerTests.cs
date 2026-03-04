using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Options;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Tests.Services;

[TestClass]
public class WopiSessionTrackerTests
{
    private static WopiSessionTracker CreateTracker(int maxSessions = 5, int tokenLifetimeMinutes = 480)
    {
        var options = MsOptions.Options.Create(new CollaboraOptions
        {
            MaxConcurrentSessions = maxSessions,
            TokenLifetimeMinutes = tokenLifetimeMinutes
        });
        return new WopiSessionTracker(options, NullLogger<WopiSessionTracker>.Instance);
    }

    [TestMethod]
    public void TryBeginSession_UnderLimit_ReturnsTrue()
    {
        var tracker = CreateTracker(maxSessions: 5);
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = tracker.TryBeginSession(fileId, userId);

        Assert.IsTrue(result);
        Assert.AreEqual(1, tracker.GetActiveSessionCount());
    }

    [TestMethod]
    public void TryBeginSession_AtCapacity_ReturnsFalse()
    {
        var tracker = CreateTracker(maxSessions: 2);

        tracker.TryBeginSession(Guid.NewGuid(), Guid.NewGuid());
        tracker.TryBeginSession(Guid.NewGuid(), Guid.NewGuid());

        var result = tracker.TryBeginSession(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsFalse(result);
        Assert.AreEqual(2, tracker.GetActiveSessionCount());
    }

    [TestMethod]
    public void TryBeginSession_SameUserSameFile_RefreshesAndReturnsTrueWithoutConsuming()
    {
        var tracker = CreateTracker(maxSessions: 1);
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        tracker.TryBeginSession(fileId, userId);

        // Same user, same file — should not consume a second slot
        var result = tracker.TryBeginSession(fileId, userId);

        Assert.IsTrue(result);
        Assert.AreEqual(1, tracker.GetActiveSessionCount());
    }

    [TestMethod]
    public void TryBeginSession_UnlimitedSessions_AlwaysReturnsTrue()
    {
        var tracker = CreateTracker(maxSessions: 0);

        for (var i = 0; i < 100; i++)
            Assert.IsTrue(tracker.TryBeginSession(Guid.NewGuid(), Guid.NewGuid()));

        Assert.AreEqual(100, tracker.GetActiveSessionCount());
    }

    [TestMethod]
    public void EndSession_ExistingSession_DecreasesCount()
    {
        var tracker = CreateTracker();
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        tracker.TryBeginSession(fileId, userId);
        Assert.AreEqual(1, tracker.GetActiveSessionCount());

        tracker.EndSession(fileId, userId);
        Assert.AreEqual(0, tracker.GetActiveSessionCount());
    }

    [TestMethod]
    public void EndSession_NonExistentSession_DoesNotThrow()
    {
        var tracker = CreateTracker();

        // Should not throw
        tracker.EndSession(Guid.NewGuid(), Guid.NewGuid());
        Assert.AreEqual(0, tracker.GetActiveSessionCount());
    }

    [TestMethod]
    public void HeartbeatSession_ExistingSession_DoesNotThrow()
    {
        var tracker = CreateTracker();
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        tracker.TryBeginSession(fileId, userId);
        tracker.HeartbeatSession(fileId, userId); // Should update last-activity without throwing

        Assert.AreEqual(1, tracker.GetActiveSessionCount());
    }

    [TestMethod]
    public void HeartbeatSession_NonExistentSession_DoesNotThrow()
    {
        var tracker = CreateTracker();
        tracker.HeartbeatSession(Guid.NewGuid(), Guid.NewGuid()); // no-op
    }

    [TestMethod]
    public void GetActiveSessionCount_AfterMultipleOperations_ReturnsCorrectCount()
    {
        var tracker = CreateTracker(maxSessions: 10);

        var id1 = (Guid.NewGuid(), Guid.NewGuid());
        var id2 = (Guid.NewGuid(), Guid.NewGuid());
        var id3 = (Guid.NewGuid(), Guid.NewGuid());

        tracker.TryBeginSession(id1.Item1, id1.Item2);
        tracker.TryBeginSession(id2.Item1, id2.Item2);
        tracker.TryBeginSession(id3.Item1, id3.Item2);
        tracker.EndSession(id2.Item1, id2.Item2);

        Assert.AreEqual(2, tracker.GetActiveSessionCount());
    }

    [TestMethod]
    public void TryBeginSession_AfterEndSession_CanAddNewSession()
    {
        var tracker = CreateTracker(maxSessions: 1);
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        tracker.TryBeginSession(fileId, userId);
        tracker.EndSession(fileId, userId);

        // Should now have a free slot
        var result = tracker.TryBeginSession(Guid.NewGuid(), Guid.NewGuid());

        Assert.IsTrue(result);
    }
}
