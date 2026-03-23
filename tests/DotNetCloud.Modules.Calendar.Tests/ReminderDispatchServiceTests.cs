using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.DTOs;
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
    private Mock<IEventBus> _eventBusMock = null!;
    private ReminderDispatchService _service = null!;
    private IServiceScopeFactory _scopeFactory = null!;
    private Guid _userId;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<CalendarDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new CalendarDbContext(options);
        _eventBusMock = new Mock<IEventBus>();
        _userId = Guid.NewGuid();

        // Build a real service provider for the scoped factory
        var services = new ServiceCollection();
        services.AddDbContext<CalendarDbContext>(o => o.UseInMemoryDatabase(_db.Database.GetConnectionString() ?? Guid.NewGuid().ToString()));
        services.AddSingleton<IEventBus>(_eventBusMock.Object);
        services.AddSingleton<IRecurrenceEngine>(new RecurrenceEngine(NullLogger<RecurrenceEngine>.Instance));

        // We need a shared DB, so let's use the same in-memory name
        var dbName = Guid.NewGuid().ToString();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDbContext<CalendarDbContext>(o => o.UseInMemoryDatabase(dbName));
        serviceCollection.AddSingleton<IEventBus>(_eventBusMock.Object);
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

        // Should have published both CalendarReminderTriggeredEvent and ReminderTriggeredEvent
        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<CalendarReminderTriggeredEvent>(e =>
                    e.CalendarEventId == evt.Id && e.UserId == _userId),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<ReminderTriggeredEvent>(e =>
                    e.EntityId == evt.Id &&
                    e.EntityType == "CalendarEvent" &&
                    e.SourceModuleId == "dotnetcloud.calendar"),
                It.IsAny<CallerContext>(),
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
    public async Task ScanAndDispatch_AlreadyFiredReminder_DoesNotDuplicate()
    {
        var cal = CreateCalendar();
        var evt = CreateEvent(cal, "No Dup", DateTime.UtcNow.AddMinutes(5), TimeSpan.FromHours(1));
        var reminder = new EventReminder { MinutesBefore = 15, Method = ReminderMethod.Notification };
        evt.Reminders.Add(reminder);
        await _db.SaveChangesAsync();

        // Fire once
        await _service.ScanAndDispatchAsync(CancellationToken.None);
        _eventBusMock.Invocations.Clear();

        // Fire again — should NOT publish again
        await _service.ScanAndDispatchAsync(CancellationToken.None);

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.IsAny<CalendarReminderTriggeredEvent>(),
                It.IsAny<CallerContext>(),
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

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.IsAny<CalendarReminderTriggeredEvent>(),
                It.IsAny<CallerContext>(),
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

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.IsAny<CalendarReminderTriggeredEvent>(),
                It.IsAny<CallerContext>(),
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

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.IsAny<CalendarReminderTriggeredEvent>(),
                It.IsAny<CallerContext>(),
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
        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.Is<CalendarReminderTriggeredEvent>(e => e.CalendarEventId == evt.Id),
                It.IsAny<CallerContext>(),
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

        _eventBusMock.Verify(
            eb => eb.PublishAsync(
                It.IsAny<CalendarReminderTriggeredEvent>(),
                It.IsAny<CallerContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
