using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Chat.Data.SqlServer.Migrations
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
                name: "Announcements",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    RequiresAcknowledgement = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlockedUsers",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlockedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Channels",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Topic = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementAcknowledgements",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnnouncementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementAcknowledgements_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalSchema: "core",
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelInvites",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelInvites_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "core",
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelMembers",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReadMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsMuted = table.Column<bool>(type: "bit", nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    NotificationPref = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelMembers_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "core",
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false),
                    ReplyToMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "core",
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_Messages_ReplyToMessageId",
                        column: x => x.ReplyToMessageId,
                        principalSchema: "core",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VideoCalls",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndReason = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MaxParticipants = table.Column<int>(type: "int", nullable: false),
                    IsGroupCall = table.Column<bool>(type: "bit", nullable: false),
                    LiveKitRoomId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoCalls_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "core",
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageAttachments",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageAttachments_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "core",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageMentions",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentionedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StartIndex = table.Column<int>(type: "int", nullable: false),
                    Length = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageMentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageMentions_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "core",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MessageReactions",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReactedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageReactions_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "core",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PinnedMessages",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PinnedMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PinnedMessages_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalSchema: "core",
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PinnedMessages_Messages_MessageId",
                        column: x => x.MessageId,
                        principalSchema: "core",
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CallParticipants",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VideoCallId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    InvitedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LeftAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HasAudio = table.Column<bool>(type: "bit", nullable: false),
                    HasVideo = table.Column<bool>(type: "bit", nullable: false),
                    HasScreenShare = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallParticipants_VideoCalls_VideoCallId",
                        column: x => x.VideoCallId,
                        principalSchema: "core",
                        principalTable: "VideoCalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAcknowledgements_AnnouncementId_UserId",
                schema: "core",
                table: "AnnouncementAcknowledgements",
                columns: new[] { "AnnouncementId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_OrganizationId",
                schema: "core",
                table: "Announcements",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_PublishedAt",
                schema: "core",
                table: "Announcements",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "ix_chat_blocked_users_blocked_user_id",
                schema: "core",
                table: "BlockedUsers",
                column: "BlockedUserId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_blocked_users_user_blocked",
                schema: "core",
                table: "BlockedUsers",
                columns: new[] { "UserId", "BlockedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_call_participants_call_user",
                schema: "core",
                table: "CallParticipants",
                columns: new[] { "VideoCallId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_call_participants_user_id",
                schema: "core",
                table: "CallParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_call_participants_user_joined",
                schema: "core",
                table: "CallParticipants",
                columns: new[] { "UserId", "JoinedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_channel_invites_channel_user_status",
                schema: "core",
                table: "ChannelInvites",
                columns: new[] { "ChannelId", "InvitedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_channel_invites_invited_by",
                schema: "core",
                table: "ChannelInvites",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_channel_invites_invited_user",
                schema: "core",
                table: "ChannelInvites",
                column: "InvitedUserId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_channel_members_channel_user",
                schema: "core",
                table: "ChannelMembers",
                columns: new[] { "ChannelId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_channel_members_user_id",
                schema: "core",
                table: "ChannelMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_channels_created_by",
                schema: "core",
                table: "Channels",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_channels_is_deleted",
                schema: "core",
                table: "Channels",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_chat_channels_last_activity",
                schema: "core",
                table: "Channels",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "ix_chat_channels_org_name_unique",
                schema: "core",
                table: "Channels",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_channels_organization_id",
                schema: "core",
                table: "Channels",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_channels_type",
                schema: "core",
                table: "Channels",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "ix_chat_message_attachments_file_node_id",
                schema: "core",
                table: "MessageAttachments",
                column: "FileNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageAttachments_MessageId",
                schema: "core",
                table: "MessageAttachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_message_mentions_mentioned_user_id",
                schema: "core",
                table: "MessageMentions",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageMentions_MessageId",
                schema: "core",
                table: "MessageMentions",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_message_reactions_message_user_emoji",
                schema: "core",
                table: "MessageReactions",
                columns: new[] { "MessageId", "UserId", "Emoji" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_channel_sent",
                schema: "core",
                table: "Messages",
                columns: new[] { "ChannelId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_is_deleted",
                schema: "core",
                table: "Messages",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_sender",
                schema: "core",
                table: "Messages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ReplyToMessageId",
                schema: "core",
                table: "Messages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_pinned_messages_channel_message",
                schema: "core",
                table: "PinnedMessages",
                columns: new[] { "ChannelId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PinnedMessages_MessageId",
                schema: "core",
                table: "PinnedMessages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_channel_state",
                schema: "core",
                table: "VideoCalls",
                columns: new[] { "ChannelId", "State" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_created_at",
                schema: "core",
                table: "VideoCalls",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_host_user_id",
                schema: "core",
                table: "VideoCalls",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_initiator_user_id",
                schema: "core",
                table: "VideoCalls",
                column: "InitiatorUserId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_is_deleted",
                schema: "core",
                table: "VideoCalls",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_state",
                schema: "core",
                table: "VideoCalls",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnouncementAcknowledgements",
                schema: "core");

            migrationBuilder.DropTable(
                name: "BlockedUsers",
                schema: "core");

            migrationBuilder.DropTable(
                name: "CallParticipants",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ChannelInvites",
                schema: "core");

            migrationBuilder.DropTable(
                name: "ChannelMembers",
                schema: "core");

            migrationBuilder.DropTable(
                name: "MessageAttachments",
                schema: "core");

            migrationBuilder.DropTable(
                name: "MessageMentions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "MessageReactions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "PinnedMessages",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Announcements",
                schema: "core");

            migrationBuilder.DropTable(
                name: "VideoCalls",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Messages",
                schema: "core");

            migrationBuilder.DropTable(
                name: "Channels",
                schema: "core");
        }
    }
}
