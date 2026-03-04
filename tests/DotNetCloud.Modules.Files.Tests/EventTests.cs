using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;

namespace DotNetCloud.Modules.Files.Tests;

/// <summary>
/// Tests for Files module events verifying IEvent contracts and record semantics.
/// </summary>
[TestClass]
public class EventTests
{
    [TestMethod]
    public void WhenFileUploadedEventCreatedThenImplementsIEvent()
    {
        var evt = new FileUploadedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "test.txt",
            UploadedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void WhenFileDeletedEventCreatedThenImplementsIEvent()
    {
        var evt = new FileDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "test.txt",
            DeletedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void WhenFileMovedEventCreatedThenImplementsIEvent()
    {
        var evt = new FileMovedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "test.txt",
            MovedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void WhenFileSharedEventCreatedThenImplementsIEvent()
    {
        var evt = new FileSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "test.txt",
            ShareId = Guid.NewGuid(),
            ShareType = "User",
            SharedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void WhenFileRestoredEventCreatedThenImplementsIEvent()
    {
        var evt = new FileRestoredEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "test.txt",
            RestoredByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void WhenFileUploadedEventCreatedThenPropertiesAreSet()
    {
        var eventId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var evt = new FileUploadedEvent
        {
            EventId = eventId,
            CreatedAt = now,
            FileNodeId = nodeId,
            FileName = "report.pdf",
            Size = 1024,
            MimeType = "application/pdf",
            ParentId = parentId,
            UploadedByUserId = userId
        };

        Assert.AreEqual(eventId, evt.EventId);
        Assert.AreEqual(now, evt.CreatedAt);
        Assert.AreEqual(nodeId, evt.FileNodeId);
        Assert.AreEqual("report.pdf", evt.FileName);
        Assert.AreEqual(1024, evt.Size);
        Assert.AreEqual("application/pdf", evt.MimeType);
        Assert.AreEqual(parentId, evt.ParentId);
        Assert.AreEqual(userId, evt.UploadedByUserId);
    }

    [TestMethod]
    public void WhenFileDeletedEventCreatedThenIsPermanentDefaultsFalse()
    {
        var evt = new FileDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "test.txt",
            DeletedByUserId = Guid.NewGuid()
        };

        Assert.IsFalse(evt.IsPermanent);
    }

    [TestMethod]
    public void WhenFileUploadedEventsWithSameValuesCreatedThenAreEqual()
    {
        var eventId = Guid.NewGuid();
        var nodeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var evt1 = new FileUploadedEvent
        {
            EventId = eventId, CreatedAt = now, FileNodeId = nodeId,
            FileName = "test.txt", UploadedByUserId = userId
        };
        var evt2 = new FileUploadedEvent
        {
            EventId = eventId, CreatedAt = now, FileNodeId = nodeId,
            FileName = "test.txt", UploadedByUserId = userId
        };

        Assert.AreEqual(evt1, evt2);
    }

    [TestMethod]
    public void WhenFileMovedEventCreatedThenPreviousAndNewParentAreTracked()
    {
        var prevParent = Guid.NewGuid();
        var newParent = Guid.NewGuid();

        var evt = new FileMovedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "doc.txt",
            PreviousParentId = prevParent,
            NewParentId = newParent,
            MovedByUserId = Guid.NewGuid()
        };

        Assert.AreEqual(prevParent, evt.PreviousParentId);
        Assert.AreEqual(newParent, evt.NewParentId);
    }

    [TestMethod]
    public void WhenFileSharedEventCreatedThenShareDetailsAreTracked()
    {
        var shareId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var evt = new FileSharedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.NewGuid(),
            FileName = "shared.txt",
            ShareId = shareId,
            ShareType = "PublicLink",
            SharedWithUserId = targetUserId,
            SharedByUserId = Guid.NewGuid()
        };

        Assert.AreEqual(shareId, evt.ShareId);
        Assert.AreEqual("PublicLink", evt.ShareType);
        Assert.AreEqual(targetUserId, evt.SharedWithUserId);
    }
}
