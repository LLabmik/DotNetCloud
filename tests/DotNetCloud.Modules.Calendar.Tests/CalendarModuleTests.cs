using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// Tests for <see cref="CalendarModule"/> lifecycle and manifest.
/// </summary>
[TestClass]
public class CalendarModuleTests
{
    [TestMethod]
    public void Manifest_HasCorrectId()
    {
        var module = new CalendarModule();

        Assert.AreEqual("dotnetcloud.calendar", module.Manifest.Id);
    }

    [TestMethod]
    public void Manifest_HasCorrectName()
    {
        var module = new CalendarModule();

        Assert.AreEqual("Calendar", module.Manifest.Name);
    }

    [TestMethod]
    public void Manifest_RequiresExpectedCapabilities()
    {
        var module = new CalendarModule();

        Assert.IsTrue(module.Manifest.RequiredCapabilities.Contains("INotificationService"));
        Assert.IsTrue(module.Manifest.RequiredCapabilities.Contains("IUserDirectory"));
        Assert.IsTrue(module.Manifest.RequiredCapabilities.Contains("ICurrentUserContext"));
    }

    [TestMethod]
    public void Manifest_PublishesCalendarEvents()
    {
        var module = new CalendarModule();

        Assert.IsTrue(module.Manifest.PublishedEvents.Contains("CalendarEventCreatedEvent"));
        Assert.IsTrue(module.Manifest.PublishedEvents.Contains("CalendarEventUpdatedEvent"));
        Assert.IsTrue(module.Manifest.PublishedEvents.Contains("CalendarEventDeletedEvent"));
        Assert.IsTrue(module.Manifest.PublishedEvents.Contains("CalendarEventRsvpEvent"));
        Assert.IsTrue(module.Manifest.PublishedEvents.Contains("CalendarReminderTriggeredEvent"));
    }

    [TestMethod]
    public void NewModule_IsNotInitialized()
    {
        var module = new CalendarModule();

        Assert.IsFalse(module.IsInitialized);
        Assert.IsFalse(module.IsRunning);
    }

    [TestMethod]
    public async Task StartAsync_WithoutInitialize_Throws()
    {
        var module = new CalendarModule();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => module.StartAsync());
    }
}
