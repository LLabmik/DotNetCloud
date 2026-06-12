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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "test.txt",
            UploadedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void WhenFileDeletedEventCreatedThenImplementsIEvent()
    {
        var evt = new FileDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "test.txt",
            DeletedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void WhenFileMovedEventCreatedThenImplementsIEvent()
    {
        var evt = new FileMovedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "test.txt",
            MovedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void WhenFileSharedEventCreatedThenImplementsIEvent()
    {
        var evt = new FileSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "test.txt",
            ShareId = Guid.CreateVersion7(),
            ShareType = "User",
            SharedByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void WhenFileRestoredEventCreatedThenImplementsIEvent()
    {
        var evt = new FileRestoredEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "test.txt",
            RestoredByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void WhenFileVersionRestoredEventCreatedThenImplementsIEvent()
    {
        var evt = new FileVersionRestoredEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "report.pdf",
            SourceVersionId = Guid.CreateVersion7(),
            SourceVersionNumber = 2,
            NewVersionNumber = 3,
            RestoredByUserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType<IEvent>(evt);
    }

    [TestMethod]
    public void WhenFileVersionRestoredEventCreatedThenVersionNumbersAreTracked()
    {
        var sourceId = Guid.CreateVersion7();

        var evt = new FileVersionRestoredEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "doc.txt",
            SourceVersionId = sourceId,
            SourceVersionNumber = 5,
            NewVersionNumber = 6,
            RestoredByUserId = Guid.CreateVersion7()
        };

        Assert.AreEqual(sourceId, evt.SourceVersionId);
        Assert.AreEqual(5, evt.SourceVersionNumber);
        Assert.AreEqual(6, evt.NewVersionNumber);
    }

    [TestMethod]
    public void WhenFileUploadedEventCreatedThenPropertiesAreSet()
    {
        var eventId = Guid.CreateVersion7();
        var nodeId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var parentId = Guid.CreateVersion7();
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "test.txt",
            DeletedByUserId = Guid.CreateVersion7()
        };

        Assert.IsFalse(evt.IsPermanent);
    }

    [TestMethod]
    public void WhenFileUploadedEventsWithSameValuesCreatedThenAreEqual()
    {
        var eventId = Guid.CreateVersion7();
        var nodeId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;

        var evt1 = new FileUploadedEvent
        {
            EventId = eventId,
            CreatedAt = now,
            FileNodeId = nodeId,
            FileName = "test.txt",
            UploadedByUserId = userId
        };
        var evt2 = new FileUploadedEvent
        {
            EventId = eventId,
            CreatedAt = now,
            FileNodeId = nodeId,
            FileName = "test.txt",
            UploadedByUserId = userId
        };

        Assert.AreEqual(evt1, evt2);
    }

    [TestMethod]
    public void WhenFileMovedEventCreatedThenPreviousAndNewParentAreTracked()
    {
        var prevParent = Guid.CreateVersion7();
        var newParent = Guid.CreateVersion7();

        var evt = new FileMovedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "doc.txt",
            PreviousParentId = prevParent,
            NewParentId = newParent,
            MovedByUserId = Guid.CreateVersion7()
        };

        Assert.AreEqual(prevParent, evt.PreviousParentId);
        Assert.AreEqual(newParent, evt.NewParentId);
    }

    [TestMethod]
    public void WhenFileSharedEventCreatedThenShareDetailsAreTracked()
    {
        var shareId = Guid.CreateVersion7();
        var targetUserId = Guid.CreateVersion7();

        var evt = new FileSharedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            FileNodeId = Guid.CreateVersion7(),
            FileName = "shared.txt",
            ShareId = shareId,
            ShareType = "PublicLink",
            SharedWithUserId = targetUserId,
            SharedByUserId = Guid.CreateVersion7()
        };

        Assert.AreEqual(shareId, evt.ShareId);
        Assert.AreEqual("PublicLink", evt.ShareType);
        Assert.AreEqual(targetUserId, evt.SharedWithUserId);
    }
}
