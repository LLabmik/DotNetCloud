namespace DotNetCloud.Core.Tests.Events;

using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Events;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contract tests for Tracks (Project Management) event types.
/// </summary>
[TestClass]
public class TracksEventTests
{
    // -- Product Events --

    [TestMethod]
    public void ProductCreatedEvent_ImplementsIEvent()
    {
        var e = new ProductCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ProductId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            OwnerId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.EventId);
        Assert.AreNotEqual(Guid.Empty, e.ProductId);
    }

    [TestMethod]
    public void ProductDeletedEvent_ImplementsIEvent()
    {
        var e = new ProductDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ProductId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.ProductId);
    }

    // -- WorkItem Events --

    [TestMethod]
    public void WorkItemCreatedEvent_ImplementsIEvent()
    {
        var e = new WorkItemCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            ProductId = Guid.NewGuid(),
            Type = WorkItemType.Item
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual(WorkItemType.Item, e.Type);
    }

    [TestMethod]
    public void WorkItemMovedEvent_ImplementsIEvent()
    {
        var fromSwimlane = Guid.NewGuid();
        var toSwimlane = Guid.NewGuid();
        var e = new WorkItemMovedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            Type = WorkItemType.Item,
            FromSwimlaneId = fromSwimlane,
            ToSwimlaneId = toSwimlane
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(e.FromSwimlaneId, e.ToSwimlaneId);
    }

    [TestMethod]
    public void WorkItemUpdatedEvent_ImplementsIEvent()
    {
        var e = new WorkItemUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            Type = WorkItemType.Item
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
    }

    [TestMethod]
    public void WorkItemDeletedEvent_ImplementsIEvent()
    {
        var e = new WorkItemDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            Type = WorkItemType.Item
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
    }

    // -- Assignment and Comment Events --

    [TestMethod]
    public void WorkItemAssignedEvent_ImplementsIEvent()
    {
        var e = new WorkItemAssignedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.UserId);
    }

    [TestMethod]
    public void WorkItemCommentAddedEvent_ImplementsIEvent()
    {
        var e = new WorkItemCommentAddedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CommentId = Guid.NewGuid(),
            WorkItemId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.CommentId);
    }

    // -- Sprint Events --

    [TestMethod]
    public void SprintStartedEvent_ImplementsIEvent()
    {
        var e = new SprintStartedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SprintId = Guid.NewGuid(),
            EpicId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.SprintId);
    }

    [TestMethod]
    public void SprintCompletedEvent_ImplementsIEvent()
    {
        var e = new SprintCompletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SprintId = Guid.NewGuid(),
            EpicId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.SprintId);
    }

    // -- Event uniqueness --

    [TestMethod]
    public void TracksEvents_HaveUniqueEventIds()
    {
        var events = new IEvent[]
        {
            new ProductCreatedEvent { EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, ProductId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), OwnerId = Guid.NewGuid() },
            new WorkItemCreatedEvent { EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, WorkItemId = Guid.NewGuid(), ProductId = Guid.NewGuid(), Type = WorkItemType.Item },
            new SprintStartedEvent { EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, SprintId = Guid.NewGuid(), EpicId = Guid.NewGuid() }
        };

        var ids = events.Select(e => e.EventId).ToHashSet();
        Assert.AreEqual(events.Length, ids.Count, "All events should have unique EventIds");
    }

    // -- Planning Poker Events --

    [TestMethod]
    public void PokerSessionStartedEvent_ImplementsIEvent()
    {
        var e = new PokerSessionStartedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SessionId = Guid.NewGuid(),
            EpicId = Guid.NewGuid(),
            ItemId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.SessionId);
        Assert.AreNotEqual(Guid.Empty, e.ItemId);
    }

    [TestMethod]
    public void PokerSessionRevealedEvent_ImplementsIEvent()
    {
        var e = new PokerSessionRevealedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SessionId = Guid.NewGuid(),
            EpicId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.SessionId);
    }

    [TestMethod]
    public void PokerSessionCompletedEvent_ImplementsIEvent()
    {
        var e = new PokerSessionCompletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SessionId = Guid.NewGuid(),
            EpicId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.SessionId);
    }
}
