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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ProductId = Guid.CreateVersion7(),
            OrganizationId = Guid.CreateVersion7(),
            OwnerId = Guid.CreateVersion7()
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ProductId = Guid.CreateVersion7()
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
            ProductId = Guid.CreateVersion7(),
            Type = WorkItemType.Item
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual(WorkItemType.Item, e.Type);
    }

    [TestMethod]
    public void WorkItemMovedEvent_ImplementsIEvent()
    {
        var fromSwimlane = Guid.CreateVersion7();
        var toSwimlane = Guid.CreateVersion7();
        var e = new WorkItemMovedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
            Type = WorkItemType.Item
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
    }

    [TestMethod]
    public void WorkItemDeletedEvent_ImplementsIEvent()
    {
        var e = new WorkItemDeletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            WorkItemId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.UserId);
    }

    [TestMethod]
    public void WorkItemCommentAddedEvent_ImplementsIEvent()
    {
        var e = new WorkItemCommentAddedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            CommentId = Guid.CreateVersion7(),
            WorkItemId = Guid.CreateVersion7(),
            UserId = Guid.CreateVersion7()
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SprintId = Guid.CreateVersion7(),
            EpicId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.SprintId);
    }

    [TestMethod]
    public void SprintCompletedEvent_ImplementsIEvent()
    {
        var e = new SprintCompletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SprintId = Guid.CreateVersion7(),
            EpicId = Guid.CreateVersion7()
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
            new ProductCreatedEvent { EventId = Guid.CreateVersion7(), CreatedAt = DateTime.UtcNow, ProductId = Guid.CreateVersion7(), OrganizationId = Guid.CreateVersion7(), OwnerId = Guid.CreateVersion7() },
            new WorkItemCreatedEvent { EventId = Guid.CreateVersion7(), CreatedAt = DateTime.UtcNow, WorkItemId = Guid.CreateVersion7(), ProductId = Guid.CreateVersion7(), Type = WorkItemType.Item },
            new SprintStartedEvent { EventId = Guid.CreateVersion7(), CreatedAt = DateTime.UtcNow, SprintId = Guid.CreateVersion7(), EpicId = Guid.CreateVersion7() }
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SessionId = Guid.CreateVersion7(),
            EpicId = Guid.CreateVersion7(),
            ItemId = Guid.CreateVersion7()
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
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SessionId = Guid.CreateVersion7(),
            EpicId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.SessionId);
    }

    [TestMethod]
    public void PokerSessionCompletedEvent_ImplementsIEvent()
    {
        var e = new PokerSessionCompletedEvent
        {
            EventId = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            SessionId = Guid.CreateVersion7(),
            EpicId = Guid.CreateVersion7()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.SessionId);
    }
}
