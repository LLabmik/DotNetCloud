using DotNetCloud.Core.Events.Search;

namespace DotNetCloud.Core.Tests.Events.Search;

/// <summary>
/// Tests for the <see cref="SearchIndexRequestEvent"/> record and <see cref="SearchIndexAction"/> enum.
/// </summary>
[TestClass]
public class SearchIndexRequestEventTests
{
    [TestMethod]
    public void SearchIndexRequestEvent_CanBeCreated_ForIndexAction()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var evt = new SearchIndexRequestEvent
        {
            EventId = eventId,
            CreatedAt = now,
            ModuleId = "notes",
            EntityId = Guid.NewGuid().ToString(),
            Action = SearchIndexAction.Index
        };

        // Assert
        Assert.AreEqual(eventId, evt.EventId);
        Assert.AreEqual(now, evt.CreatedAt);
        Assert.AreEqual("notes", evt.ModuleId);
        Assert.AreEqual(SearchIndexAction.Index, evt.Action);
    }

    [TestMethod]
    public void SearchIndexRequestEvent_CanBeCreated_ForRemoveAction()
    {
        // Act
        var evt = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "files",
            EntityId = Guid.NewGuid().ToString(),
            Action = SearchIndexAction.Remove
        };

        // Assert
        Assert.AreEqual(SearchIndexAction.Remove, evt.Action);
        Assert.AreEqual("files", evt.ModuleId);
    }

    [TestMethod]
    public void SearchIndexRequestEvent_ImplementsIEvent()
    {
        // Arrange
        var evt = new SearchIndexRequestEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            ModuleId = "chat",
            EntityId = Guid.NewGuid().ToString(),
            Action = SearchIndexAction.Index
        };

        // Assert
        Assert.IsInstanceOfType<DotNetCloud.Core.Events.IEvent>(evt);
    }

    [TestMethod]
    public void SearchIndexAction_HasExpectedValues()
    {
        // Assert
        Assert.AreEqual(0, (int)SearchIndexAction.Index);
        Assert.AreEqual(1, (int)SearchIndexAction.Remove);
    }

    [TestMethod]
    public void SearchIndexAction_CanParse_FromString()
    {
        // Act & Assert
        Assert.AreEqual(SearchIndexAction.Index, Enum.Parse<SearchIndexAction>("Index"));
        Assert.AreEqual(SearchIndexAction.Remove, Enum.Parse<SearchIndexAction>("Remove"));
    }
}
