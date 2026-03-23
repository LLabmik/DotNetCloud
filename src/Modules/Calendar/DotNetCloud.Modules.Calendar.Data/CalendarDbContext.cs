using DotNetCloud.Modules.Calendar.Data.Configuration;
using DotNetCloud.Modules.Calendar.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Calendar.Data;

/// <summary>
/// Database context for the Calendar module.
/// Manages all calendar entities: calendars, events, attendees, reminders, and shares.
/// </summary>
public class CalendarDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarDbContext"/> class.
    /// </summary>
    public CalendarDbContext(DbContextOptions<CalendarDbContext> options)
        : base(options)
    {
    }

    /// <summary>Calendar collections.</summary>
    public DbSet<Models.Calendar> Calendars => Set<Models.Calendar>();

    /// <summary>Calendar events.</summary>
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    /// <summary>Event attendees.</summary>
    public DbSet<EventAttendee> EventAttendees => Set<EventAttendee>();

    /// <summary>Event reminders.</summary>
    public DbSet<EventReminder> EventReminders => Set<EventReminder>();

    /// <summary>Calendar sharing grants.</summary>
    public DbSet<CalendarShare> CalendarShares => Set<CalendarShare>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new CalendarConfiguration());
        modelBuilder.ApplyConfiguration(new CalendarEventConfiguration());
        modelBuilder.ApplyConfiguration(new EventAttendeeConfiguration());
        modelBuilder.ApplyConfiguration(new EventReminderConfiguration());
        modelBuilder.ApplyConfiguration(new CalendarShareConfiguration());
    }
}
