using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Video.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "video");

            migrationBuilder.CreateTable(
                name: "VideoCollections",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Videos",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subtitles",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subtitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subtitles_Videos_VideoId",
                        column: x => x.VideoId,
                        principalSchema: "video",
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoCollectionItems",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoCollectionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoCollectionItems_VideoCollections_CollectionId",
                        column: x => x.CollectionId,
                        principalSchema: "video",
                        principalTable: "VideoCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoCollectionItems_Videos_VideoId",
                        column: x => x.VideoId,
                        principalSchema: "video",
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoMetadata",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    FrameRate = table.Column<double>(type: "double precision", nullable: false),
                    VideoCodec = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AudioCodec = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Bitrate = table.Column<long>(type: "bigint", nullable: false),
                    AudioTrackCount = table.Column<int>(type: "integer", nullable: false),
                    SubtitleTrackCount = table.Column<int>(type: "integer", nullable: false),
                    ContainerFormat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoMetadata_Videos_VideoId",
                        column: x => x.VideoId,
                        principalSchema: "video",
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoShares",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedWithUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Permission = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ShareToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoShares_Videos_VideoId",
                        column: x => x.VideoId,
                        principalSchema: "video",
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchHistories",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    WatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DurationWatchedSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchHistories_Videos_VideoId",
                        column: x => x.VideoId,
                        principalSchema: "video",
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchProgresses",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionTicks = table.Column<long>(type: "bigint", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchProgresses_Videos_VideoId",
                        column: x => x.VideoId,
                        principalSchema: "video",
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subtitles_video_id",
                schema: "video",
                table: "Subtitles",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "ix_subtitles_video_language",
                schema: "video",
                table: "Subtitles",
                columns: new[] { "VideoId", "Language" });

            migrationBuilder.CreateIndex(
                name: "ix_collection_items_collection_id",
                schema: "video",
                table: "VideoCollectionItems",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "ix_collection_items_video_id",
                schema: "video",
                table: "VideoCollectionItems",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "uq_collection_items_collection_video",
                schema: "video",
                table: "VideoCollectionItems",
                columns: new[] { "CollectionId", "VideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_video_collections_name",
                schema: "video",
                table: "VideoCollections",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "ix_video_collections_owner_id",
                schema: "video",
                table: "VideoCollections",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "uq_video_metadata_video_id",
                schema: "video",
                table: "VideoMetadata",
                column: "VideoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_videos_is_deleted",
                schema: "video",
                table: "Videos",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_videos_owner_created_at",
                schema: "video",
                table: "Videos",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_videos_owner_id",
                schema: "video",
                table: "Videos",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_videos_title",
                schema: "video",
                table: "Videos",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "uq_videos_file_node_id",
                schema: "video",
                table: "Videos",
                column: "FileNodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_video_shares_shared_by",
                schema: "video",
                table: "VideoShares",
                column: "SharedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_video_shares_shared_with",
                schema: "video",
                table: "VideoShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "ix_video_shares_token",
                schema: "video",
                table: "VideoShares",
                column: "ShareToken");

            migrationBuilder.CreateIndex(
                name: "ix_video_shares_video_id",
                schema: "video",
                table: "VideoShares",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "ix_watch_history_user_id",
                schema: "video",
                table: "WatchHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_watch_history_user_watched_at",
                schema: "video",
                table: "WatchHistories",
                columns: new[] { "UserId", "WatchedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_watch_history_video_id",
                schema: "video",
                table: "WatchHistories",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "ix_watch_progress_user_id",
                schema: "video",
                table: "WatchProgresses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchProgresses_VideoId",
                schema: "video",
                table: "WatchProgresses",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "uq_watch_progress_user_video",
                schema: "video",
                table: "WatchProgresses",
                columns: new[] { "UserId", "VideoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Subtitles",
                schema: "video");

            migrationBuilder.DropTable(
                name: "VideoCollectionItems",
                schema: "video");

            migrationBuilder.DropTable(
                name: "VideoMetadata",
                schema: "video");

            migrationBuilder.DropTable(
                name: "VideoShares",
                schema: "video");

            migrationBuilder.DropTable(
                name: "WatchHistories",
                schema: "video");

            migrationBuilder.DropTable(
                name: "WatchProgresses",
                schema: "video");

            migrationBuilder.DropTable(
                name: "VideoCollections",
                schema: "video");

            migrationBuilder.DropTable(
                name: "Videos",
                schema: "video");
        }
    }
}
