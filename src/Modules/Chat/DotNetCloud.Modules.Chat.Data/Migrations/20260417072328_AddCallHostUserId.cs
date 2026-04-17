using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Chat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCallHostUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HostUserId",
                table: "VideoCalls",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Set HostUserId to InitiatorUserId for all existing calls
            migrationBuilder.Sql(
                """UPDATE "VideoCalls" SET "HostUserId" = "InitiatorUserId" WHERE "HostUserId" = '00000000-0000-0000-0000-000000000000'""");

            // Rename stored enum string values: "Initiator" → "Host"
            migrationBuilder.Sql(
                """UPDATE "CallParticipants" SET "Role" = 'Host' WHERE "Role" = 'Initiator'""");

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_host_user_id",
                table: "VideoCalls",
                column: "HostUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_chat_video_calls_host_user_id",
                table: "VideoCalls");

            migrationBuilder.DropColumn(
                name: "HostUserId",
                table: "VideoCalls");

            // Revert stored enum string values: "Host" → "Initiator"
            migrationBuilder.Sql(
                """UPDATE "CallParticipants" SET "Role" = 'Initiator' WHERE "Role" = 'Host'""");
        }
    }
}
