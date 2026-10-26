using DotNetCloud.Core.DTOs;
using DotNetCloud.Core.Errors;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Calendar.Data;
using DotNetCloud.Modules.Calendar.Data.Services;
using DotNetCloud.Modules.Calendar.Models;
using DotNetCloud.Modules.Calendar.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DotNetCloud.Modules.Calendar.Tests;

/// <summary>
/// Tests for <see cref="ReminderDispatchService"/> — background reminder scanning and dispatch.
/// </summary>
[TestClass]
public class ReminderDispatchServiceTests
{
    private CalendarDbContext _db = null!;
    private Mock<IPlatformEventPublisher> _publisherMock = null!;
    private ReminderDispatchService _service = null!;
    private IServiceScopeFactory _scopeFactory = null!;
    private Guid _userId;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;

        _db = new CalendarDbContext(options);
        _publisherMock = new Mock<IPlatformEventPublisher>();
        _userId = Guid.CreateVersion7();

        // The scan cycle resolves its dependencies from a scope, so the collection is the only
        // place the publisher and recurrence engine need to be registered.
        var dbName = Guid.CreateVersion7().ToString();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContext<CalendarDbContext>(o => o.UseInMemoryDatabase(dbName));
        serviceCollection.AddSingleton<IPlatformEventPublisher>(_publisherMock.Object);
        serviceCollection.AddSingleton<IRecurrenceEngine>(new RecurrenceEngine(NullLogger<RecurrenceEngine>.Instance));
        var sp = serviceCollection.BuildServiceProvider();
        _scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        // Reinitialize _db using the same in-memory db name
        _db.Dispose();
        _db = new CalendarDbContext(new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

        _service = new ReminderDispatchService(_scopeFactory, NullLogger<ReminderDispatchService>.Instance);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _db.Dispose();
    }

    private Models.Calendar CreateCalendar()
    {
        var cal = new Models.Calendar
        {
            OwnerId = _userId,
            Name = "Test Calendar"
        };
        _db.Calendars.Add(cal);
        return cal;
    }

    private CalendarEvent CreateEvent(Models.Calendar calendar, string title, DateTime startUtc, TimeSpan duration, string? rrule = null)
    {
        var evt = new CalendarEvent
        {
            CalendarId = calendar.Id,
            Calendar = calendar,
            CreatedByUserId = _userId,
            Title = title,
            StartUtc = startUtc,
            EndUtc = startUtc + duration
        };
        if (rrule is not null)
        {
            evt.RecurrenceRule = rrule;
        }
        _db.CalendarEvents.Add(evt);
        return evt;
    }

    // ─── SINGLE EVENT REMINDERS ──────────────────────────────

    [TestMethod]
    public async Task ScanAndDispatch_DueReminder_PublishesEvents()
    {
        var cal = CreateCalendar();
        var evt = CreateEvent(cal, "Meeting", DateTime.UtcNow.AddMinutes(5), TimeSpan.FromHours(1));
        evt.Reminders.Add(new EventReminder { MinutesBefore = 15, Method = ReminderMethod.Notification });
        await _db.SaveChangesAsync();

        await _service.ScanAndDispatchAsync(CancellationToken.None);

        // One platform event per due reminder — the core turns it into the notification.
        _publisherMock.Verify(
            pub => pub.PublishAsync(
                It.Is<ReminderTriggeredEvent>(e =>
                    e.EntityId == evt.Id &&
                    e.EntityType == "CalendarEvent" &&
                    e.UserId == _userId &&
                    e.SourceModuleId == ReminderDispatchService.ModuleId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ScanAndDispatch_DueReminder_CreatesLog()
    {
        var cal = CreateCalendar();
        var evt = CreateEvent(cal, "Log Test", DateTime.UtcNow.AddMinutes(5), TimeSpan.FromHours(1));
        var reminder = new EventReminder { MinutesBefore = 15, Method = ReminderMethod.Notification };
        evt.Reminders.Add(reminder);
        await _db.SaveChangesAsync();

        await _service.ScanAndDispatchAsync(CancellationToken.None);

        // Re-read from the DB used by the service (via scope factory)
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        var logs = await db.ReminderLogs.ToListAsync();

        Assert.AreEqual(1, logs.Count);
        Assert.AreEqual(reminder.Id, logs[0].ReminderId);
        Assert.IsTrue(logs[0].Success);
    }

    [TestMethod]
    public async Task ScanAndDispatch_PublishFails_RecordsFailureAndDoesNotMarkDelivered()
    {
        var cal = CreateCalendar();
        var evt = CreateEvent(cal, "Core Down", DateTime.UtcNow.AddMinutes(5), TimeSpan.FromHours(1));
        var reminder = new EventReminder { MinutesBefore = 15, Method = ReminderMethod.Notification };
        evt.Reminders.Add(reminder);
        await _db.SaveChangesAsync();

        _publisherMock
            .Setup(pub => pub.PublishAsync(
                It.IsAny<ReminderTriggeredEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PlatformEventPublishException("ReminderTriggeredEvent", "core unavailable"));

        await _service.ScanAndDispatchAsync(CancellationToken.None);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        var logs = await db.ReminderLogs.ToListAsync();

        // The reminder was not delivered, so the log must say so — and because only successful rows
        // suppress a re-fire, the next scan retries it.
        Assert.AreEqual(1, logs.Count);
        Assert.IsFalse(logs[0].Success);
        Assert.IsTrue(logs[0].ErrorMessage!.Contains("core unavailable"));
    }

    [TestMethod]
    public async Task ScanAndDispatch_AlreadyFiredReminder_DoesNotDuplicate()
    {
        var cal = CreateCalendar();
        var evt = CreateEvent(cal, "No Dup", DateTime.UtcNow.AddMinutes(5), TimeSpan.FromHours(1));
        var reminder = new EventReminder { MinutesBefore = 15, Method = ReminderMethod.Notification };
        evt.Reminders.Add(reminder);
        await _db.SaveChangesAsync();

        // Fire once
        await _service.ScanAndDispatchAsync(CancellationToken.None);
        _publisherMock.Invocations.Clear();

        // Fire again — should NOT publish again
        await _service.ScanAndDispatchAsync(CancellationToken.None);

        _publisherMock.Verify(
            pub => pub.PublishAsync(
                It.IsAny<ReminderTriggeredEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ScanAndDispatch_FutureReminder_DoesNotFire()
    {
        var cal = CreateCalendar();
        // Event is 2 hours from now, reminder is 15 min before — not yet due
        var evt = CreateEvent(cal, "Future", DateTime.UtcNow.AddHours(2), TimeSpan.FromHours(1));
        evt.Reminders.Add(new EventReminder { MinutesBefore = 15, Method = ReminderMethod.Notification });
        await _db.SaveChangesAsync();

        await _service.ScanAndDispatchAsync(CancellationToken.None);

        _publisherMock.Verify(
            pub => pub.PublishAsync(
                It.IsAny<ReminderTriggeredEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ScanAndDispatch_DeletedEvent_IsSkipped()
    {
        var cal = CreateCalendar();
        var evt = CreateEvent(cal, "Deleted", DateTime.UtcNow.AddMinutes(5), TimeSpan.FromHours(1));
        evt.Reminders.Add(new EventReminder { MinutesBefore = 15, Method = ReminderMethod.Notification });
        evt.IsDeleted = true;
        evt.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _service.ScanAndDispatchAsync(CancellationToken.None);

        _publisherMock.Verify(
            pub => pub.PublishAsync(
                It.IsAny<ReminderTriggeredEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ScanAndDispatch_MultipleReminders_FiresEachOnce()
    {
        var cal = CreateCalendar();
        var evt = CreateEvent(cal, "Multi", DateTime.UtcNow.AddMinutes(5), TimeSpan.FromHours(1));
        evt.Reminders.Add(new EventReminder { MinutesBefore = 15, Method = ReminderMethod.Notification });
        evt.Reminders.Add(new EventReminder { MinutesBefore = 30, Method = ReminderMethod.Email });
        await _db.SaveChangesAsync();

        await _service.ScanAndDispatchAsync(CancellationToken.None);

        _publisherMock.Verify(
            pub => pub.PublishAsync(
                It.IsAny<ReminderTriggeredEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    // ─── RECURRING EVENT REMINDERS ──────────────────────────

    [TestMethod]
    public async Task ScanAndDispatch_RecurringEvent_FiresForDueOccurrence()
    {
        var cal = CreateCalendar();
        // Daily event starting now, reminder 15 min before
        var evt = CreateEvent(cal, "Daily Standup",
            DateTime.UtcNow.AddMinutes(5), TimeSpan.FromMinutes(30), "FREQ=DAILY;COUNT=5");
        evt.Reminders.Add(new EventReminder { MinutesBefore = 15, Method = ReminderMethod.Notification });
        await _db.SaveChangesAsync();

        await _service.ScanAndDispatchAsync(CancellationToken.None);

        // Should fire for the upcoming occurrence
        _publisherMock.Verify(
            pub => pub.PublishAsync(
                It.Is<ReminderTriggeredEvent>(e => e.EntityId == evt.Id),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task ScanAndDispatch_EventWithNoReminders_IsIgnored()
    {
        var cal = CreateCalendar();
        CreateEvent(cal, "No Reminders", DateTime.UtcNow.AddMinutes(5), TimeSpan.FromHours(1));
        await _db.SaveChangesAsync();

        await _service.ScanAndDispatchAsync(CancellationToken.None);

        _publisherMock.Verify(
            pub => pub.PublishAsync(
                It.IsAny<ReminderTriggeredEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── LONG-LEAD REMINDER WINDOW ─────────────────────────

    [TestMethod]
    public async Task ResolveLookAheadWindow_NoReminders_FallsBackToTheMinimumWindow()
    {
        var cal = CreateCalendar();
        CreateEvent(cal, "No Reminders", DateTime.UtcNow.AddMinutes(5), TimeSpan.FromHours(1));
        await _db.SaveChangesAsync();

        var window = await ReminderDispatchService.ResolveLookAheadWindowAsync(_db, CancellationToken.None);

        Assert.AreEqual(ReminderDispatchService.MinimumLookAheadWindow, window);
    }

    [TestMethod]
    public async Task ResolveLookAheadWindow_LongLeadReminder_ExtendsToTheLargestOffset()
    {
        var cal = CreateCalendar();
        var evt = CreateEvent(cal, "Rush Concert", DateTime.UtcNow.AddDays(7), TimeSpan.FromHours(2));
        evt.Reminders.Add(new EventReminder { MinutesBefore = 10080, Method = ReminderMethod.Notification });
        await _db.SaveChangesAsync();

        var window = await ReminderDispatchService.ResolveLookAheadWindowAsync(_db, CancellationToken.None);

        Assert.AreEqual(TimeSpan.FromDays(7), window);
    }

    [TestMethod]
    public async Task ResolveLookAheadWindow_ExtremeOffset_IsClamped()
    {
        var cal = CreateCalendar();
        var evt = CreateEvent(cal, "Absurd", DateTime.UtcNow.AddDays(1), TimeSpan.FromHours(1));
        evt.Reminders.Add(new EventReminder { MinutesBefore = 400 * 24 * 60, Method = ReminderMethod.Notification });
        await _db.SaveChangesAsync();

        var window = await ReminderDispatchService.ResolveLookAheadWindowAsync(_db, CancellationToken.None);

        Assert.AreEqual(ReminderDispatchService.MaximumLookAheadWindow, window);
    }

    /// <summary>
    /// Regression: a reminder one week before an event one week away is due now, but the event sat
    /// outside the old fixed 24-hour window, so it was never even loaded and the reminder fired about
    /// six days late (when the event came within 24 hours).
    /// </summary>
    [TestMethod]
    public async Task ScanAndDispatch_LongLeadReminder_FiresWhenDue()
    {
        var cal = CreateCalendar();
        var evt = CreateEvent(cal, "Rush Concert", DateTime.UtcNow.AddDays(7).AddMinutes(-1), TimeSpan.FromHours(2));
        evt.Reminders.Add(new EventReminder { MinutesBefore = 10080, Method = ReminderMethod.Notification });
        await _db.SaveChangesAsync();

        await _service.ScanAndDispatchAsync(CancellationToken.None);

        _publisherMock.Verify(
            pub => pub.PublishAsync(
                It.Is<ReminderTriggeredEvent>(e => e.EntityId == evt.Id),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ScanAndDispatch_LongLeadReminder_NotYetDue_DoesNotFire()
    {
        var cal = CreateCalendar();
        // 10 days out with a 7-day reminder: the trigger is still 3 days away.
        var evt = CreateEvent(cal, "Rush Concert", DateTime.UtcNow.AddDays(10), TimeSpan.FromHours(2));
        evt.Reminders.Add(new EventReminder { MinutesBefore = 10080, Method = ReminderMethod.Notification });
        await _db.SaveChangesAsync();

        await _service.ScanAndDispatchAsync(CancellationToken.None);

        _publisherMock.Verify(
            pub => pub.PublishAsync(
                It.IsAny<ReminderTriggeredEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
