using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Chat.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.RenameTable(
                name: "VideoCalls",
                newName: "VideoCalls",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "PinnedMessages",
                newName: "PinnedMessages",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "Messages",
                newName: "Messages",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "MessageReactions",
                newName: "MessageReactions",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "MessageMentions",
                newName: "MessageMentions",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "MessageAttachments",
                newName: "MessageAttachments",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "Channels",
                newName: "Channels",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "ChannelMembers",
                newName: "ChannelMembers",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "ChannelInvites",
                newName: "ChannelInvites",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "CallParticipants",
                newName: "CallParticipants",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "BlockedUsers",
                newName: "BlockedUsers",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "Announcements",
                newName: "Announcements",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "AnnouncementAcknowledgements",
                newName: "AnnouncementAcknowledgements",
                newSchema: "core");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "VideoCalls",
                schema: "core",
                newName: "VideoCalls");

            migrationBuilder.RenameTable(
                name: "PinnedMessages",
                schema: "core",
                newName: "PinnedMessages");

            migrationBuilder.RenameTable(
                name: "Messages",
                schema: "core",
                newName: "Messages");

            migrationBuilder.RenameTable(
                name: "MessageReactions",
                schema: "core",
                newName: "MessageReactions");

            migrationBuilder.RenameTable(
                name: "MessageMentions",
                schema: "core",
                newName: "MessageMentions");

            migrationBuilder.RenameTable(
                name: "MessageAttachments",
                schema: "core",
                newName: "MessageAttachments");

            migrationBuilder.RenameTable(
                name: "Channels",
                schema: "core",
                newName: "Channels");

            migrationBuilder.RenameTable(
                name: "ChannelMembers",
                schema: "core",
                newName: "ChannelMembers");

            migrationBuilder.RenameTable(
                name: "ChannelInvites",
                schema: "core",
                newName: "ChannelInvites");

            migrationBuilder.RenameTable(
                name: "CallParticipants",
                schema: "core",
                newName: "CallParticipants");

            migrationBuilder.RenameTable(
                name: "BlockedUsers",
                schema: "core",
                newName: "BlockedUsers");

            migrationBuilder.RenameTable(
                name: "Announcements",
                schema: "core",
                newName: "Announcements");

            migrationBuilder.RenameTable(
                name: "AnnouncementAcknowledgements",
                schema: "core",
                newName: "AnnouncementAcknowledgements");
        }
    }
}
