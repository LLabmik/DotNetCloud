using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Calendar.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "calendar");

            migrationBuilder.RenameTable(
                name: "ReminderLogs",
                newName: "ReminderLogs",
                newSchema: "calendar");

            migrationBuilder.RenameTable(
                name: "EventReminders",
                newName: "EventReminders",
                newSchema: "calendar");

            migrationBuilder.RenameTable(
                name: "EventAttendees",
                newName: "EventAttendees",
                newSchema: "calendar");

            migrationBuilder.RenameTable(
                name: "CalendarShares",
                newName: "CalendarShares",
                newSchema: "calendar");

            migrationBuilder.RenameTable(
                name: "Calendars",
                newName: "Calendars",
                newSchema: "calendar");

            migrationBuilder.RenameTable(
                name: "CalendarEvents",
                newName: "CalendarEvents",
                newSchema: "calendar");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "ReminderLogs",
                schema: "calendar",
                newName: "ReminderLogs");

            migrationBuilder.RenameTable(
                name: "EventReminders",
                schema: "calendar",
                newName: "EventReminders");

            migrationBuilder.RenameTable(
                name: "EventAttendees",
                schema: "calendar",
                newName: "EventAttendees");

            migrationBuilder.RenameTable(
                name: "CalendarShares",
                schema: "calendar",
                newName: "CalendarShares");

            migrationBuilder.RenameTable(
                name: "Calendars",
                schema: "calendar",
                newName: "Calendars");

            migrationBuilder.RenameTable(
                name: "CalendarEvents",
                schema: "calendar",
                newName: "CalendarEvents");
        }
    }
}
