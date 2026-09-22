using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Chat.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageArchiving_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                schema: "core",
                table: "Messages",
                type: "datetime2",
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
