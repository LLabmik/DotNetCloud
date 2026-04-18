namespace DotNetCloud.Core.Tests.Events;

using DotNetCloud.Core.Events;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contract tests for Tracks (Project Management) event types.
/// </summary>
[TestClass]
public class TracksEventTests
{
    // ── Board Events ──

    [TestMethod]
    public void BoardCreatedEvent_ImplementsIEvent()
    {
        var e = new BoardCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            BoardId = Guid.NewGuid(),
            Title = "Sprint Board",
            OwnerId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.EventId);
        Assert.AreEqual("Sprint Board", e.Title);
    }

    [TestMethod]
    public void BoardDeletedEvent_ImplementsIEvent()
    {
        var e = new BoardDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            BoardId = Guid.NewGuid(),
            DeletedByUserId = Guid.NewGuid(),
            IsPermanent = true
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.IsTrue(e.IsPermanent);
    }

    // ── Card Events ──

    [TestMethod]
    public void CardCreatedEvent_ImplementsIEvent()
    {
        var e = new CardCreatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            Title = "Fix login",
            BoardId = Guid.NewGuid(),
            SwimlaneId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual("Fix login", e.Title);
    }

    [TestMethod]
    public void CardMovedEvent_ImplementsIEvent()
    {
        var fromSwimlane = Guid.NewGuid();
        var toSwimlane = Guid.NewGuid();
        var e = new CardMovedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            FromSwimlaneId = fromSwimlane,
            ToSwimlaneId = toSwimlane,
            MovedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(e.FromSwimlaneId, e.ToSwimlaneId);
    }

    [TestMethod]
    public void CardUpdatedEvent_ImplementsIEvent()
    {
        var e = new CardUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            UpdatedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
    }

    [TestMethod]
    public void CardDeletedEvent_ImplementsIEvent()
    {
        var e = new CardDeletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            DeletedByUserId = Guid.NewGuid(),
            IsPermanent = false
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.IsFalse(e.IsPermanent);
    }

    // ── Assignment & Comment Events ──

    [TestMethod]
    public void CardAssignedEvent_ImplementsIEvent()
    {
        var e = new CardAssignedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            AssignedUserId = Guid.NewGuid(),
            AssignedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.AssignedUserId);
    }

    [TestMethod]
    public void CardCommentAddedEvent_ImplementsIEvent()
    {
        var e = new CardCommentAddedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CommentId = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.CommentId);
    }

    // ── Sprint Events ──

    [TestMethod]
    public void SprintStartedEvent_ImplementsIEvent()
    {
        var e = new SprintStartedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SprintId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = "Sprint 1",
            StartedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual("Sprint 1", e.Title);
    }

    [TestMethod]
    public void SprintCompletedEvent_ImplementsIEvent()
    {
        var e = new SprintCompletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SprintId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Title = "Sprint 1",
            CompletedByUserId = Guid.NewGuid(),
            CompletedCardCount = 8,
            TotalCardCount = 10
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual(8, e.CompletedCardCount);
        Assert.AreEqual(10, e.TotalCardCount);
    }

    // ── Event uniqueness ──

    [TestMethod]
    public void TracksEvents_HaveUniqueEventIds()
    {
        var events = new IEvent[]
        {
            new BoardCreatedEvent { EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, BoardId = Guid.NewGuid(), Title = "A", OwnerId = Guid.NewGuid() },
            new CardCreatedEvent { EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, CardId = Guid.NewGuid(), Title = "B", BoardId = Guid.NewGuid(), SwimlaneId = Guid.NewGuid(), CreatedByUserId = Guid.NewGuid() },
            new SprintStartedEvent { EventId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, SprintId = Guid.NewGuid(), BoardId = Guid.NewGuid(), Title = "C", StartedByUserId = Guid.NewGuid() }
        };

        var ids = events.Select(e => e.EventId).ToHashSet();
        Assert.AreEqual(events.Length, ids.Count, "All events should have unique EventIds");
    }

    // ── Planning Poker Events ──

    [TestMethod]
    public void PokerSessionStartedEvent_ImplementsIEvent()
    {
        var e = new PokerSessionStartedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SessionId = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            StartedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreNotEqual(Guid.Empty, e.SessionId);
        Assert.AreNotEqual(Guid.Empty, e.CardId);
    }

    [TestMethod]
    public void PokerSessionRevealedEvent_ImplementsIEvent()
    {
        var e = new PokerSessionRevealedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SessionId = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            VoteCount = 5
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual(5, e.VoteCount);
    }

    [TestMethod]
    public void PokerSessionCompletedEvent_ImplementsIEvent()
    {
        var e = new PokerSessionCompletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SessionId = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            AcceptedEstimate = "8",
            StoryPoints = 8,
            AcceptedByUserId = Guid.NewGuid()
        };

        Assert.IsInstanceOfType(e, typeof(IEvent));
        Assert.AreEqual("8", e.AcceptedEstimate);
        Assert.AreEqual(8, e.StoryPoints);
    }

    [TestMethod]
    public void PokerSessionCompletedEvent_NonNumericEstimate_StoryPointsNull()
    {
        var e = new PokerSessionCompletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            SessionId = Guid.NewGuid(),
            CardId = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            AcceptedEstimate = "XL",
            StoryPoints = null,
            AcceptedByUserId = Guid.NewGuid()
        };

        Assert.IsNull(e.StoryPoints);
        Assert.AreEqual("XL", e.AcceptedEstimate);
    }
}
