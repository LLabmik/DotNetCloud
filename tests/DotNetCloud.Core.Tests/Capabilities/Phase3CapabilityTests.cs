namespace DotNetCloud.Core.Tests.Capabilities;

using DotNetCloud.Core.Capabilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contract tests for Phase 3 capability interfaces.
/// </summary>
[TestClass]
public class Phase3CapabilityTests
{
    [TestMethod]
    public void IContactDirectory_ExtendsICapabilityInterface()
    {
        // Assert
        Assert.IsTrue(typeof(ICapabilityInterface).IsAssignableFrom(typeof(IContactDirectory)));
    }

    [TestMethod]
    public void IContactDirectory_HasRequiredMethods()
    {
        // Assert
        var type = typeof(IContactDirectory);
        Assert.IsNotNull(type.GetMethod("GetContactDisplayNameAsync"));
        Assert.IsNotNull(type.GetMethod("GetContactDisplayNamesAsync"));
        Assert.IsNotNull(type.GetMethod("SearchContactsAsync"));
    }

    [TestMethod]
    public void ICalendarDirectory_ExtendsICapabilityInterface()
    {
        // Assert
        Assert.IsTrue(typeof(ICapabilityInterface).IsAssignableFrom(typeof(ICalendarDirectory)));
    }

    [TestMethod]
    public void ICalendarDirectory_HasRequiredMethods()
    {
        // Assert
        var type = typeof(ICalendarDirectory);
        Assert.IsNotNull(type.GetMethod("GetEventSummaryAsync"));
        Assert.IsNotNull(type.GetMethod("GetUpcomingEventsAsync"));
    }

    [TestMethod]
    public void INoteDirectory_ExtendsICapabilityInterface()
    {
        // Assert
        Assert.IsTrue(typeof(ICapabilityInterface).IsAssignableFrom(typeof(INoteDirectory)));
    }

    [TestMethod]
    public void INoteDirectory_HasRequiredMethods()
    {
        // Assert
        var type = typeof(INoteDirectory);
        Assert.IsNotNull(type.GetMethod("GetNoteTitleAsync"));
        Assert.IsNotNull(type.GetMethod("GetNoteTitlesAsync"));
        Assert.IsNotNull(type.GetMethod("SearchNotesAsync"));
    }

    [TestMethod]
    public void CalendarEventSummary_CanBeCreated()
    {
        // Arrange & Act
        var summary = new CalendarEventSummary
        {
            Id = Guid.NewGuid(),
            Title = "Quick Sync",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddMinutes(30),
            IsAllDay = false
        };

        // Assert
        Assert.AreEqual("Quick Sync", summary.Title);
        Assert.IsFalse(summary.IsAllDay);
    }

    [TestMethod]
    public void CalendarEventSummary_IsSealed()
    {
        Assert.IsTrue(typeof(CalendarEventSummary).IsSealed);
    }
}
