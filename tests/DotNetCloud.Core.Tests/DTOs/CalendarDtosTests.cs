namespace DotNetCloud.Core.Tests.DTOs;

using DotNetCloud.Core.DTOs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Contract tests for Calendar DTOs.
/// </summary>
[TestClass]
public class CalendarDtosTests
{
    [TestMethod]
    public void CalendarDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange & Act
        var calendar = new CalendarDto
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Name = "Work Calendar",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreNotEqual(Guid.Empty, calendar.Id);
        Assert.AreEqual("Work Calendar", calendar.Name);
        Assert.AreEqual("UTC", calendar.Timezone);
        Assert.IsFalse(calendar.IsDefault);
        Assert.IsTrue(calendar.IsVisible);
    }

    [TestMethod]
    public void CalendarEventDto_CanBeCreated_WithRequiredProperties()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddHours(1);

        // Act
        var calEvent = new CalendarEventDto
        {
            Id = Guid.NewGuid(),
            CalendarId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            Title = "Team Standup",
            StartUtc = start,
            EndUtc = end,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.AreEqual("Team Standup", calEvent.Title);
        Assert.AreEqual(CalendarEventStatus.Confirmed, calEvent.Status);
        Assert.IsFalse(calEvent.IsAllDay);
        Assert.IsNull(calEvent.RecurrenceRule);
        Assert.AreEqual(0, calEvent.Attendees.Count);
        Assert.AreEqual(0, calEvent.Reminders.Count);
    }

    [TestMethod]
    public void CalendarEventStatus_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(CalendarEventStatus));

        // Assert
        Assert.AreEqual(3, values.Length);
    }

    [TestMethod]
    public void AttendeeRole_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(AttendeeRole));

        // Assert
        Assert.AreEqual(3, values.Length);
    }

    [TestMethod]
    public void AttendeeStatus_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(AttendeeStatus));

        // Assert
        Assert.AreEqual(4, values.Length);
    }

    [TestMethod]
    public void ReminderMethod_AllValuesExist()
    {
        // Act
        var values = Enum.GetValues(typeof(ReminderMethod));

        // Assert
        Assert.AreEqual(2, values.Length);
    }

    [TestMethod]
    public void EventAttendeeDto_HasRequiredProperties()
    {
        // Arrange & Act
        var attendee = new EventAttendeeDto
        {
            UserId = Guid.NewGuid(),
            Email = "attendee@example.com",
            DisplayName = "Test User"
        };

        // Assert
        Assert.AreEqual("attendee@example.com", attendee.Email);
        Assert.AreEqual(AttendeeRole.Required, attendee.Role);
        Assert.AreEqual(AttendeeStatus.NeedsAction, attendee.Status);
    }

    [TestMethod]
    public void EventReminderDto_HasRequiredProperties()
    {
        // Arrange & Act
        var reminder = new EventReminderDto { MinutesBefore = 15 };

        // Assert
        Assert.AreEqual(15, reminder.MinutesBefore);
        Assert.AreEqual(ReminderMethod.Notification, reminder.Method);
    }

    [TestMethod]
    public void CreateCalendarDto_HasRequiredProperties()
    {
        // Arrange & Act
        var dto = new CreateCalendarDto { Name = "Personal" };

        // Assert
        Assert.AreEqual("Personal", dto.Name);
        Assert.AreEqual("UTC", dto.Timezone);
        Assert.IsNull(dto.Color);
    }

    [TestMethod]
    public void CreateCalendarEventDto_HasRequiredProperties()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddHours(2);

        // Act
        var dto = new CreateCalendarEventDto
        {
            CalendarId = Guid.NewGuid(),
            Title = "Meeting",
            StartUtc = start,
            EndUtc = end
        };

        // Assert
        Assert.AreEqual("Meeting", dto.Title);
        Assert.IsFalse(dto.IsAllDay);
        Assert.AreEqual(0, dto.Attendees.Count);
        Assert.AreEqual(0, dto.Reminders.Count);
    }

    [TestMethod]
    public void UpdateCalendarEventDto_AllFields_AreNullable()
    {
        // Arrange & Act
        var dto = new UpdateCalendarEventDto();

        // Assert
        Assert.IsNull(dto.Title);
        Assert.IsNull(dto.StartUtc);
        Assert.IsNull(dto.EndUtc);
        Assert.IsNull(dto.Status);
        Assert.IsNull(dto.RecurrenceRule);
        Assert.IsNull(dto.Attendees);
        Assert.IsNull(dto.Reminders);
    }

    [TestMethod]
    public void EventRsvpDto_HasRequiredProperties()
    {
        // Arrange & Act
        var rsvp = new EventRsvpDto
        {
            Status = AttendeeStatus.Accepted,
            Comment = "Looking forward to it!"
        };

        // Assert
        Assert.AreEqual(AttendeeStatus.Accepted, rsvp.Status);
        Assert.AreEqual("Looking forward to it!", rsvp.Comment);
    }

    [TestMethod]
    public void CalendarEventDto_RecurrenceFields_WorkTogether()
    {
        // Arrange & Act
        var calEvent = new CalendarEventDto
        {
            Id = Guid.NewGuid(),
            CalendarId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            Title = "Weekly Sync",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1),
            RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Assert
        Assert.IsNotNull(calEvent.RecurrenceRule);
        Assert.IsNull(calEvent.RecurringEventId);
        Assert.IsNull(calEvent.OriginalStartUtc);
    }

    [TestMethod]
    public void CalendarEventDto_IsImmutableRecord()
    {
        // Arrange
        var calEvent = new CalendarEventDto
        {
            Id = Guid.NewGuid(),
            CalendarId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            Title = "Original",
            StartUtc = DateTime.UtcNow,
            EndUtc = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var updated = calEvent with { Title = "Updated" };

        // Assert
        Assert.AreEqual("Original", calEvent.Title);
        Assert.AreEqual("Updated", updated.Title);
    }
}
