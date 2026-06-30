using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Calendar.Data.Migrations
{
    /// <inheritdoc />
    public partial class SwitchCalendarToCoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.RenameTable(
                name: "ReminderLogs",
                schema: "calendar",
                newName: "ReminderLogs",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "EventReminders",
                schema: "calendar",
                newName: "EventReminders",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "EventAttendees",
                schema: "calendar",
                newName: "EventAttendees",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "CalendarShares",
                schema: "calendar",
                newName: "CalendarShares",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "Calendars",
                schema: "calendar",
                newName: "Calendars",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "CalendarEvents",
                schema: "calendar",
                newName: "CalendarEvents",
                newSchema: "core");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "calendar");

            migrationBuilder.RenameTable(
                name: "ReminderLogs",
                schema: "core",
                newName: "ReminderLogs",
                newSchema: "calendar");

            migrationBuilder.RenameTable(
                name: "EventReminders",
                schema: "core",
                newName: "EventReminders",
                newSchema: "calendar");

            migrationBuilder.RenameTable(
                name: "EventAttendees",
                schema: "core",
                newName: "EventAttendees",
                newSchema: "calendar");

            migrationBuilder.RenameTable(
                name: "CalendarShares",
                schema: "core",
                newName: "CalendarShares",
                newSchema: "calendar");

            migrationBuilder.RenameTable(
                name: "Calendars",
                schema: "core",
                newName: "Calendars",
                newSchema: "calendar");

            migrationBuilder.RenameTable(
                name: "CalendarEvents",
                schema: "core",
                newName: "CalendarEvents",
                newSchema: "calendar");
        }
    }
}
