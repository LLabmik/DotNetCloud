using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Chat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCallParticipantStateAndInvitedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvitedAtUtc",
                table: "CallParticipants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "State",
                table: "CallParticipants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Set existing participants to Joined (1) state since they predate the State column.
            // Participants with LeftAtUtc set should be Left (2).
            migrationBuilder.Sql(
                """UPDATE "CallParticipants" SET "State" = 1 WHERE "LeftAtUtc" IS NULL""");
            migrationBuilder.Sql(
                """UPDATE "CallParticipants" SET "State" = 2 WHERE "LeftAtUtc" IS NOT NULL""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvitedAtUtc",
                table: "CallParticipants");

            migrationBuilder.DropColumn(
                name: "State",
                table: "CallParticipants");
        }
    }
}
