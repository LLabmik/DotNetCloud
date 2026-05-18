using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Calendar.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "Calendars",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Timezone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    SyncToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAllDay = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecurrenceRule = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecurringEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ETag = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_CalendarEvents_RecurringEventId",
                        column: x => x.RecurringEventId,
                        principalSchema: "core",
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalSchema: "core",
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarShares",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CalendarId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedWithUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SharedWithTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Permission = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarShares_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalSchema: "core",
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventAttendees",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventAttendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventAttendees_CalendarEvents_EventId",
                        column: x => x.EventId,
                        principalSchema: "core",
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventReminders",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MinutesBefore = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventReminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventReminders_CalendarEvents_EventId",
                        column: x => x.EventId,
                        principalSchema: "core",
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReminderLogs",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReminderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurrenceStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TriggeredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReminderLogs_EventReminders_ReminderId",
                        column: x => x.ReminderId,
                        principalSchema: "core",
                        principalTable: "EventReminders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_calendar_id",
                schema: "core",
                table: "CalendarEvents",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_calendar_start",
                schema: "core",
                table: "CalendarEvents",
                columns: new[] { "CalendarId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_created_by",
                schema: "core",
                table: "CalendarEvents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_is_deleted",
                schema: "core",
                table: "CalendarEvents",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_recurring_parent",
                schema: "core",
                table: "CalendarEvents",
                column: "RecurringEventId");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_time_range",
                schema: "core",
                table: "CalendarEvents",
                columns: new[] { "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_calendars_is_deleted",
                schema: "core",
                table: "Calendars",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_calendars_organization_id",
                schema: "core",
                table: "Calendars",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "ix_calendars_owner_id",
                schema: "core",
                table: "Calendars",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_calendars_owner_name",
                schema: "core",
                table: "Calendars",
                columns: new[] { "OwnerId", "Name" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_shares_calendar_id",
                schema: "core",
                table: "CalendarShares",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_shares_team_id",
                schema: "core",
                table: "CalendarShares",
                column: "SharedWithTeamId");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_shares_user_id",
                schema: "core",
                table: "CalendarShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "ix_event_attendees_event_email",
                schema: "core",
                table: "EventAttendees",
                columns: new[] { "EventId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_event_attendees_event_id",
                schema: "core",
                table: "EventAttendees",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "ix_event_attendees_user_id",
                schema: "core",
                table: "EventAttendees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_event_reminders_event_id",
                schema: "core",
                table: "EventReminders",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "ix_reminder_logs_event_id",
                schema: "core",
                table: "ReminderLogs",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "ix_reminder_logs_reminder_occurrence",
                schema: "core",
                table: "ReminderLogs",
                columns: new[] { "ReminderId", "OccurrenceStartUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarShares",
                schema: "core");

            migrationBuilder.DropTable(
                name: "EventAttendees",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ReminderLogs",
                schema: "core");

            migrationBuilder.DropTable(
                name: "EventReminders",
                schema: "core");

            migrationBuilder.DropTable(
                name: "CalendarEvents",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Calendars",
                schema: "core");
        }
    }
}
