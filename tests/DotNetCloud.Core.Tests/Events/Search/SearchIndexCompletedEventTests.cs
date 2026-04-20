using DotNetCloud.Core.Events.Search;

namespace DotNetCloud.Core.Tests.Events.Search;

/// <summary>
/// Tests for the <see cref="SearchIndexCompletedEvent"/> record and <see cref="IndexCompletionStatus"/> enum.
/// </summary>
[TestClass]
public class SearchIndexCompletedEventTests
{
    [TestMethod]
    public void SearchIndexCompletedEvent_CanBeCreated_ForSuccess()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Act
        var evt = new SearchIndexCompletedEvent
        {
            EventId = eventId,
            CreatedAt = now,
            Status = IndexCompletionStatus.Success,
            DocumentsProcessed = 150
        };

        // Assert
        Assert.AreEqual(eventId, evt.EventId);
        Assert.AreEqual(now, evt.CreatedAt);
        Assert.AreEqual(IndexCompletionStatus.Success, evt.Status);
        Assert.AreEqual(150, evt.DocumentsProcessed);
        Assert.IsNull(evt.ModuleId);
    }

    [TestMethod]
    public void SearchIndexCompletedEvent_CanBeCreated_ForModuleSpecificReindex()
    {
        // Act
        var evt = new SearchIndexCompletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Status = IndexCompletionStatus.PartialSuccess,
            DocumentsProcessed = 42,
            ModuleId = "files"
        };

        // Assert
        Assert.AreEqual(IndexCompletionStatus.PartialSuccess, evt.Status);
        Assert.AreEqual("files", evt.ModuleId);
        Assert.AreEqual(42, evt.DocumentsProcessed);
    }

    [TestMethod]
    public void SearchIndexCompletedEvent_ImplementsIEvent()
    {
        // Arrange
        var evt = new SearchIndexCompletedEvent
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Status = IndexCompletionStatus.Failed,
            DocumentsProcessed = 0
        };

        // Assert
        Assert.IsInstanceOfType<DotNetCloud.Core.Events.IEvent>(evt);
    }

    [TestMethod]
    public void IndexCompletionStatus_HasExpectedValues()
    {
        // Assert
        Assert.AreEqual(0, Convert.ToInt32(IndexCompletionStatus.Success));
        Assert.AreEqual(1, Convert.ToInt32(IndexCompletionStatus.PartialSuccess));
        Assert.AreEqual(2, Convert.ToInt32(IndexCompletionStatus.Failed));
    }

    [TestMethod]
    public void IndexCompletionStatus_CanParse_FromString()
    {
        // Act & Assert
        Assert.AreEqual(IndexCompletionStatus.Success, Enum.Parse<IndexCompletionStatus>("Success"));
        Assert.AreEqual(IndexCompletionStatus.PartialSuccess, Enum.Parse<IndexCompletionStatus>("PartialSuccess"));
        Assert.AreEqual(IndexCompletionStatus.Failed, Enum.Parse<IndexCompletionStatus>("Failed"));
    }
}
