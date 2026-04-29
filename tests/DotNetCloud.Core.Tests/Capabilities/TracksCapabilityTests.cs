namespace DotNetCloud.Core.Tests.Capabilities;

using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contract tests for Tracks capability interface.
/// </summary>
[TestClass]
public class TracksCapabilityTests
{
    [TestMethod]
    public void ITracksDirectory_ExtendsICapabilityInterface()
    {
        // Assert
        Assert.IsTrue(typeof(ICapabilityInterface).IsAssignableFrom(typeof(ITracksDirectory)));
    }

    [TestMethod]
    public void ITracksDirectory_HasBoardMethods()
    {
        // Assert
        var type = typeof(ITracksDirectory);
        Assert.IsNotNull(type.GetMethod("GetBoardTitleAsync"));
        Assert.IsNotNull(type.GetMethod("GetBoardTitlesAsync"));
        Assert.IsNotNull(type.GetMethod("SearchBoardsAsync"));
    }

    [TestMethod]
    public void ITracksDirectory_HasCardMethods()
    {
        // Assert
        var type = typeof(ITracksDirectory);
        Assert.IsNotNull(type.GetMethod("GetCardTitleAsync"));
        Assert.IsNotNull(type.GetMethod("GetCardTitlesAsync"));
        Assert.IsNotNull(type.GetMethod("SearchCardsAsync"));
    }

    [TestMethod]
    public void CardSummary_CanBeCreated()
    {
        // Arrange & Act
        var summary = new CardSummary
        {
            Id = Guid.NewGuid(),
            Title = "Fix the bug",
            BoardId = Guid.NewGuid(),
            BoardTitle = "Dev Board",
            Priority = Priority.High,
            DueDate = DateTime.UtcNow.AddDays(3)
        };

        // Assert
        Assert.AreEqual("Fix the bug", summary.Title);
        Assert.AreEqual("Dev Board", summary.BoardTitle);
        Assert.AreEqual(Priority.High, summary.Priority);
        Assert.IsNotNull(summary.DueDate);
    }

    [TestMethod]
    public void CardSummary_OptionalFields_DefaultCorrectly()
    {
        // Arrange & Act
        var summary = new CardSummary
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            BoardId = Guid.NewGuid(),
            BoardTitle = "Board"
        };

        // Assert
        Assert.AreEqual(Priority.None, summary.Priority);
        Assert.IsNull(summary.DueDate);
    }
}
