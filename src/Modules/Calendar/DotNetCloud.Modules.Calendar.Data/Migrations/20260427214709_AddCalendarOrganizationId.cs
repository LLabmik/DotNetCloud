using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Calendar.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarOrganizationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Calendars",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendars_organization_id",
                table: "Calendars",
                column: "OrganizationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_calendars_organization_id",
                table: "Calendars");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Calendars");
        }
    }
}
