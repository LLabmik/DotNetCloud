using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Chat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageArchiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                schema: "core",
                table: "Messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_channel_archived_sent",
                schema: "core",
                table: "Messages",
                columns: new[] { "ChannelId", "ArchivedAt", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_chat_messages_channel_archived_sent",
                schema: "core",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                schema: "core",
                table: "Messages");
        }
    }
}
