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
    public void ITracksDirectory_HasProductMethods()
    {
        // Assert
        var type = typeof(ITracksDirectory);
        Assert.IsNotNull(type.GetMethod("GetProductTitleAsync"));
        Assert.IsNotNull(type.GetMethod("GetProductTitlesAsync"));
        Assert.IsNotNull(type.GetMethod("SearchProductsAsync"));
    }

    [TestMethod]
    public void ITracksDirectory_HasWorkItemMethods()
    {
        // Assert
        var type = typeof(ITracksDirectory);
        Assert.IsNotNull(type.GetMethod("GetWorkItemTitleAsync"));
        Assert.IsNotNull(type.GetMethod("GetWorkItemTitlesAsync"));
        Assert.IsNotNull(type.GetMethod("SearchWorkItemsAsync"));
    }

    [TestMethod]
    public void WorkItemSummary_CanBeCreated()
    {
        // Arrange & Act
        var summary = new WorkItemSummary
        {
            Id = Guid.NewGuid(),
            Title = "Fix the bug",
            ProductId = Guid.NewGuid(),
            ProductTitle = "Dev Product",
            Priority = Priority.High,
            DueDate = DateTime.UtcNow.AddDays(3)
        };

        // Assert
        Assert.AreEqual("Fix the bug", summary.Title);
        Assert.AreEqual("Dev Product", summary.ProductTitle);
        Assert.AreEqual(Priority.High, summary.Priority);
        Assert.IsNotNull(summary.DueDate);
    }

    [TestMethod]
    public void WorkItemSummary_OptionalFields_DefaultCorrectly()
    {
        // Arrange & Act
        var summary = new WorkItemSummary
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            ProductId = Guid.NewGuid(),
            ProductTitle = "Product"
        };

        // Assert
        Assert.AreEqual(Priority.None, summary.Priority);
        Assert.IsNull(summary.DueDate);
    }
}
