using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Chat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoCalling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitiatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MaxParticipants = table.Column<int>(type: "integer", nullable: false),
                    IsGroupCall = table.Column<bool>(type: "boolean", nullable: false),
                    LiveKitRoomId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoCalls_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CallParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoCallId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LeftAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HasAudio = table.Column<bool>(type: "boolean", nullable: false),
                    HasVideo = table.Column<bool>(type: "boolean", nullable: false),
                    HasScreenShare = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallParticipants_VideoCalls_VideoCallId",
                        column: x => x.VideoCallId,
                        principalTable: "VideoCalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chat_call_participants_call_user",
                table: "CallParticipants",
                columns: new[] { "VideoCallId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_call_participants_user_id",
                table: "CallParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_call_participants_user_joined",
                table: "CallParticipants",
                columns: new[] { "UserId", "JoinedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_channel_state",
                table: "VideoCalls",
                columns: new[] { "ChannelId", "State" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_created_at",
                table: "VideoCalls",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_initiator_user_id",
                table: "VideoCalls",
                column: "InitiatorUserId");

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_is_deleted",
                table: "VideoCalls",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_chat_video_calls_state",
                table: "VideoCalls",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CallParticipants");

            migrationBuilder.DropTable(
                name: "VideoCalls");
        }
    }
}
