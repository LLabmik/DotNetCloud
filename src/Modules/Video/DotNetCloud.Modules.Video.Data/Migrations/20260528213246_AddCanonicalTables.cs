using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DotNetCloud.Modules.Video.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                schema: "video",
                table: "Videos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "canonical_tmdb_data",
                schema: "video",
                columns: table => new
                {
                    TmdbId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TmdbTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Overview = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TmdbRating = table.Column<double>(type: "double precision", nullable: true),
                    Genres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExternalPosterHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastEnrichedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_tmdb_data", x => x.TmdbId);
                });

            migrationBuilder.CreateTable(
                name: "canonical_video_series",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    PosterHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TmdbId = table.Column<int>(type: "integer", nullable: true),
                    TmdbName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TmdbOverview = table.Column<string>(type: "text", nullable: true),
                    TmdbRating = table.Column<double>(type: "double precision", nullable: true),
                    Genres = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TotalSeasons = table.Column<int>(type: "integer", nullable: false),
                    TotalEpisodes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_video_series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "canonical_videos",
                schema: "video",
                columns: table => new
                {
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    ThumbnailPosterHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    HasExternalPoster = table.Column<bool>(type: "boolean", nullable: false),
                    ExternalPosterHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EmbeddedTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EmbeddedImdbId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EmbeddedTmdbId = table.Column<int>(type: "integer", nullable: true),
                    EmbeddedDate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EmbeddedLanguage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_videos", x => x.ContentHash);
                });

            migrationBuilder.CreateTable(
                name: "canonical_video_seasons",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Overview = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PosterHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TmdbId = table.Column<int>(type: "integer", nullable: true),
                    EpisodeCount = table.Column<int>(type: "integer", nullable: false),
                    AirDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_video_seasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_canonical_video_seasons_canonical_video_series_SeriesId",
                        column: x => x.SeriesId,
                        principalSchema: "video",
                        principalTable: "canonical_video_series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "canonical_video_series_items",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    EpisodeTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_video_series_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_canonical_video_series_items_canonical_video_series_SeriesId",
                        column: x => x.SeriesId,
                        principalSchema: "video",
                        principalTable: "canonical_video_series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "canonical_subtitles",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_subtitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_canonical_subtitles_canonical_videos_VideoContentHash",
                        column: x => x.VideoContentHash,
                        principalSchema: "video",
                        principalTable: "canonical_videos",
                        principalColumn: "ContentHash",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "canonical_video_metadata",
                schema: "video",
                columns: table => new
                {
                    VideoContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_canonical_video_metadata", x => x.VideoContentHash);
                    table.ForeignKey(
                        name: "FK_canonical_video_metadata_canonical_videos_VideoContentHash",
                        column: x => x.VideoContentHash,
                        principalSchema: "video",
                        principalTable: "canonical_videos",
                        principalColumn: "ContentHash",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_videos",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_videos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_videos_canonical_videos_CanonicalContentHash",
                        column: x => x.CanonicalContentHash,
                        principalSchema: "video",
                        principalTable: "canonical_videos",
                        principalColumn: "ContentHash",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "canonical_video_episodes",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EpisodeNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Overview = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_video_episodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_canonical_video_episodes_canonical_video_seasons_SeasonId",
                        column: x => x.SeasonId,
                        principalSchema: "video",
                        principalTable: "canonical_video_seasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_videos_content_hash",
                schema: "video",
                table: "Videos",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_subtitles_video_content_hash",
                schema: "video",
                table: "canonical_subtitles",
                column: "VideoContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_video_episodes_season_id",
                schema: "video",
                table: "canonical_video_episodes",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_video_episodes_video_content_hash",
                schema: "video",
                table: "canonical_video_episodes",
                column: "VideoContentHash");

            migrationBuilder.CreateIndex(
                name: "uq_canonical_video_episodes_season_episode",
                schema: "video",
                table: "canonical_video_episodes",
                columns: new[] { "SeasonId", "EpisodeNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_canonical_video_seasons_series_id",
                schema: "video",
                table: "canonical_video_seasons",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "uq_canonical_video_seasons_series_season",
                schema: "video",
                table: "canonical_video_seasons",
                columns: new[] { "SeriesId", "SeasonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_canonical_video_series_name",
                schema: "video",
                table: "canonical_video_series",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_video_series_tmdb_id",
                schema: "video",
                table: "canonical_video_series",
                column: "TmdbId");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_video_series_items_series_id",
                schema: "video",
                table: "canonical_video_series_items",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_video_series_items_video_content_hash",
                schema: "video",
                table: "canonical_video_series_items",
                column: "VideoContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_videos_embedded_imdb_id",
                schema: "video",
                table: "canonical_videos",
                column: "EmbeddedImdbId");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_videos_embedded_tmdb_id",
                schema: "video",
                table: "canonical_videos",
                column: "EmbeddedTmdbId");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_videos_title",
                schema: "video",
                table: "canonical_videos",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "ix_user_videos_canonical_content_hash",
                schema: "video",
                table: "user_videos",
                column: "CanonicalContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_user_videos_is_deleted",
                schema: "video",
                table: "user_videos",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_user_videos_owner_created_at",
                schema: "video",
                table: "user_videos",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_user_videos_owner_id",
                schema: "video",
                table: "user_videos",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "uq_user_videos_file_node_owner_id",
                schema: "video",
                table: "user_videos",
                columns: new[] { "FileNodeId", "OwnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "canonical_subtitles",
                schema: "video");

            migrationBuilder.DropTable(
                name: "canonical_tmdb_data",
                schema: "video");

            migrationBuilder.DropTable(
                name: "canonical_video_episodes",
                schema: "video");

            migrationBuilder.DropTable(
                name: "canonical_video_metadata",
                schema: "video");

            migrationBuilder.DropTable(
                name: "canonical_video_series_items",
                schema: "video");

            migrationBuilder.DropTable(
                name: "user_videos",
                schema: "video");

            migrationBuilder.DropTable(
                name: "canonical_video_seasons",
                schema: "video");

            migrationBuilder.DropTable(
                name: "canonical_videos",
                schema: "video");

            migrationBuilder.DropTable(
                name: "canonical_video_series",
                schema: "video");

            migrationBuilder.DropIndex(
                name: "ix_videos_content_hash",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                schema: "video",
                table: "Videos");
        }
    }
}
