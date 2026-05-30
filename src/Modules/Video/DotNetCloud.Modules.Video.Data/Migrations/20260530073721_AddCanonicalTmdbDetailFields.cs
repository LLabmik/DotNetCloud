using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DotNetCloud.Modules.Video.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalTmdbDetailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_watch_history_user_id",
                schema: "video",
                table: "WatchHistories");

            migrationBuilder.DropIndex(
                name: "ix_watch_history_user_watched_at",
                schema: "video",
                table: "WatchHistories");

            migrationBuilder.DropIndex(
                name: "ix_video_shares_shared_by",
                schema: "video",
                table: "VideoShares");

            migrationBuilder.DropIndex(
                name: "ix_video_shares_shared_with",
                schema: "video",
                table: "VideoShares");

            migrationBuilder.DropIndex(
                name: "ix_video_shares_token",
                schema: "video",
                table: "VideoShares");

            migrationBuilder.DropIndex(
                name: "uq_video_series_items_series_video",
                schema: "video",
                table: "VideoSeriesItems");

            migrationBuilder.DropIndex(
                name: "ix_video_series_name",
                schema: "video",
                table: "VideoSeries");

            migrationBuilder.DropIndex(
                name: "ix_video_series_owner_id",
                schema: "video",
                table: "VideoSeries");

            migrationBuilder.DropIndex(
                name: "ix_video_series_tmdb_id",
                schema: "video",
                table: "VideoSeries");

            migrationBuilder.DropIndex(
                name: "uq_video_seasons_series_season_number",
                schema: "video",
                table: "VideoSeasons");

            migrationBuilder.DropIndex(
                name: "ix_videos_content_hash",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "ix_videos_is_deleted",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "ix_videos_last_enriched_at",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "ix_videos_owner_created_at",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "ix_videos_owner_id",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "ix_videos_title",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "ix_videos_tmdb_id",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "uq_videos_file_node_owner_id",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "uq_video_episodes_season_video",
                schema: "video",
                table: "VideoEpisodes");

            migrationBuilder.DropIndex(
                name: "ix_video_collections_name",
                schema: "video",
                table: "VideoCollections");

            migrationBuilder.DropIndex(
                name: "ix_video_collections_owner_id",
                schema: "video",
                table: "VideoCollections");

            migrationBuilder.DropIndex(
                name: "uq_collection_items_collection_video",
                schema: "video",
                table: "VideoCollectionItems");

            migrationBuilder.DropIndex(
                name: "ix_subtitles_video_language",
                schema: "video",
                table: "Subtitles");

            migrationBuilder.RenameIndex(
                name: "ix_watch_history_video_id",
                schema: "video",
                table: "WatchHistories",
                newName: "IX_WatchHistories_VideoId");

            migrationBuilder.RenameIndex(
                name: "ix_video_shares_video_id",
                schema: "video",
                table: "VideoShares",
                newName: "IX_VideoShares_VideoId");

            migrationBuilder.RenameIndex(
                name: "ix_video_series_items_video_id",
                schema: "video",
                table: "VideoSeriesItems",
                newName: "IX_VideoSeriesItems_VideoId");

            migrationBuilder.RenameIndex(
                name: "ix_video_series_items_series_id",
                schema: "video",
                table: "VideoSeriesItems",
                newName: "IX_VideoSeriesItems_SeriesId");

            migrationBuilder.RenameIndex(
                name: "ix_video_seasons_series_id",
                schema: "video",
                table: "VideoSeasons",
                newName: "IX_VideoSeasons_SeriesId");

            migrationBuilder.RenameColumn(
                name: "thumbnail_poster",
                schema: "video",
                table: "Videos",
                newName: "ThumbnailPoster");

            migrationBuilder.RenameIndex(
                name: "uq_video_metadata_video_id",
                schema: "video",
                table: "VideoMetadata",
                newName: "IX_VideoMetadata_VideoId");

            migrationBuilder.RenameIndex(
                name: "ix_video_episodes_video_id",
                schema: "video",
                table: "VideoEpisodes",
                newName: "IX_VideoEpisodes_VideoId");

            migrationBuilder.RenameIndex(
                name: "ix_video_episodes_season_id",
                schema: "video",
                table: "VideoEpisodes",
                newName: "IX_VideoEpisodes_SeasonId");

            migrationBuilder.RenameIndex(
                name: "ix_collection_items_video_id",
                schema: "video",
                table: "VideoCollectionItems",
                newName: "IX_VideoCollectionItems_VideoId");

            migrationBuilder.RenameIndex(
                name: "ix_collection_items_collection_id",
                schema: "video",
                table: "VideoCollectionItems",
                newName: "IX_VideoCollectionItems_CollectionId");

            migrationBuilder.RenameIndex(
                name: "ix_subtitles_video_id",
                schema: "video",
                table: "Subtitles",
                newName: "IX_Subtitles_VideoId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "WatchedAt",
                schema: "video",
                table: "WatchHistories",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "ShareToken",
                schema: "video",
                table: "VideoShares",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Permission",
                schema: "video",
                table: "VideoShares",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "VideoShares",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "EpisodeTitle",
                schema: "video",
                table: "VideoSeriesItems",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "AddedAt",
                schema: "video",
                table: "VideoSeriesItems",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                schema: "video",
                table: "VideoSeries",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "TmdbName",
                schema: "video",
                table: "VideoSeries",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "video",
                table: "VideoSeries",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "video",
                table: "VideoSeries",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "Genres",
                schema: "video",
                table: "VideoSeries",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExternalPosterPath",
                schema: "video",
                table: "VideoSeries",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "video",
                table: "VideoSeries",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "VideoSeries",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                schema: "video",
                table: "VideoSeasons",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "Overview",
                schema: "video",
                table: "VideoSeasons",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "video",
                table: "VideoSeasons",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "VideoSeasons",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                schema: "video",
                table: "Videos",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "TmdbTitle",
                schema: "video",
                table: "Videos",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "video",
                table: "Videos",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Overview",
                schema: "video",
                table: "Videos",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(5000)",
                oldMaxLength: 5000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MimeType",
                schema: "video",
                table: "Videos",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Genres",
                schema: "video",
                table: "Videos",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                schema: "video",
                table: "Videos",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "ExternalPosterPath",
                schema: "video",
                table: "Videos",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "Videos",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "ContentHash",
                schema: "video",
                table: "Videos",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VideoCodec",
                schema: "video",
                table: "VideoMetadata",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExtractedAt",
                schema: "video",
                table: "VideoMetadata",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "ContainerFormat",
                schema: "video",
                table: "VideoMetadata",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AudioCodec",
                schema: "video",
                table: "VideoMetadata",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "video",
                table: "VideoEpisodes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Overview",
                schema: "video",
                table: "VideoEpisodes",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "AddedAt",
                schema: "video",
                table: "VideoEpisodes",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                schema: "video",
                table: "VideoCollections",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "video",
                table: "VideoCollections",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "video",
                table: "VideoCollections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "VideoCollections",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AddedAt",
                schema: "video",
                table: "VideoCollectionItems",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                schema: "video",
                table: "Subtitles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                schema: "video",
                table: "Subtitles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Format",
                schema: "video",
                table: "Subtitles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "Subtitles",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AlterColumn<int>(
                name: "TmdbId",
                schema: "video",
                table: "canonical_tmdb_data",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "OriginalLanguage",
                schema: "video",
                table: "canonical_tmdb_data",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalTitle",
                schema: "video",
                table: "canonical_tmdb_data",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                schema: "video",
                table: "canonical_tmdb_data",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VoteCount",
                schema: "video",
                table: "canonical_tmdb_data",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_video_collections",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_video_collections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_video_collection_items",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_video_collection_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_video_collection_items_user_video_collections_Collecti~",
                        column: x => x.CollectionId,
                        principalSchema: "video",
                        principalTable: "user_video_collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_collection_items_canonical_hash",
                schema: "video",
                table: "user_video_collection_items",
                column: "CanonicalContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_user_collection_items_collection_id",
                schema: "video",
                table: "user_video_collection_items",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "uq_user_collection_items_collection_hash",
                schema: "video",
                table: "user_video_collection_items",
                columns: new[] { "CollectionId", "CanonicalContentHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_video_collections_name",
                schema: "video",
                table: "user_video_collections",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "ix_user_video_collections_owner_id",
                schema: "video",
                table: "user_video_collections",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_video_collection_items",
                schema: "video");

            migrationBuilder.DropTable(
                name: "user_video_collections",
                schema: "video");

            migrationBuilder.DropColumn(
                name: "OriginalLanguage",
                schema: "video",
                table: "canonical_tmdb_data");

            migrationBuilder.DropColumn(
                name: "OriginalTitle",
                schema: "video",
                table: "canonical_tmdb_data");

            migrationBuilder.DropColumn(
                name: "Tagline",
                schema: "video",
                table: "canonical_tmdb_data");

            migrationBuilder.DropColumn(
                name: "VoteCount",
                schema: "video",
                table: "canonical_tmdb_data");

            migrationBuilder.RenameIndex(
                name: "IX_WatchHistories_VideoId",
                schema: "video",
                table: "WatchHistories",
                newName: "ix_watch_history_video_id");

            migrationBuilder.RenameIndex(
                name: "IX_VideoShares_VideoId",
                schema: "video",
                table: "VideoShares",
                newName: "ix_video_shares_video_id");

            migrationBuilder.RenameIndex(
                name: "IX_VideoSeriesItems_VideoId",
                schema: "video",
                table: "VideoSeriesItems",
                newName: "ix_video_series_items_video_id");

            migrationBuilder.RenameIndex(
                name: "IX_VideoSeriesItems_SeriesId",
                schema: "video",
                table: "VideoSeriesItems",
                newName: "ix_video_series_items_series_id");

            migrationBuilder.RenameIndex(
                name: "IX_VideoSeasons_SeriesId",
                schema: "video",
                table: "VideoSeasons",
                newName: "ix_video_seasons_series_id");

            migrationBuilder.RenameColumn(
                name: "ThumbnailPoster",
                schema: "video",
                table: "Videos",
                newName: "thumbnail_poster");

            migrationBuilder.RenameIndex(
                name: "IX_VideoMetadata_VideoId",
                schema: "video",
                table: "VideoMetadata",
                newName: "uq_video_metadata_video_id");

            migrationBuilder.RenameIndex(
                name: "IX_VideoEpisodes_VideoId",
                schema: "video",
                table: "VideoEpisodes",
                newName: "ix_video_episodes_video_id");

            migrationBuilder.RenameIndex(
                name: "IX_VideoEpisodes_SeasonId",
                schema: "video",
                table: "VideoEpisodes",
                newName: "ix_video_episodes_season_id");

            migrationBuilder.RenameIndex(
                name: "IX_VideoCollectionItems_VideoId",
                schema: "video",
                table: "VideoCollectionItems",
                newName: "ix_collection_items_video_id");

            migrationBuilder.RenameIndex(
                name: "IX_VideoCollectionItems_CollectionId",
                schema: "video",
                table: "VideoCollectionItems",
                newName: "ix_collection_items_collection_id");

            migrationBuilder.RenameIndex(
                name: "IX_Subtitles_VideoId",
                schema: "video",
                table: "Subtitles",
                newName: "ix_subtitles_video_id");

            migrationBuilder.AlterColumn<DateTime>(
                name: "WatchedAt",
                schema: "video",
                table: "WatchHistories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "ShareToken",
                schema: "video",
                table: "VideoShares",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Permission",
                schema: "video",
                table: "VideoShares",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "VideoShares",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "EpisodeTitle",
                schema: "video",
                table: "VideoSeriesItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "AddedAt",
                schema: "video",
                table: "VideoSeriesItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                schema: "video",
                table: "VideoSeries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "TmdbName",
                schema: "video",
                table: "VideoSeries",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "video",
                table: "VideoSeries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "video",
                table: "VideoSeries",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Genres",
                schema: "video",
                table: "VideoSeries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExternalPosterPath",
                schema: "video",
                table: "VideoSeries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "video",
                table: "VideoSeries",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "VideoSeries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                schema: "video",
                table: "VideoSeasons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Overview",
                schema: "video",
                table: "VideoSeasons",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "video",
                table: "VideoSeasons",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "VideoSeasons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                schema: "video",
                table: "Videos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "TmdbTitle",
                schema: "video",
                table: "Videos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "video",
                table: "Videos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Overview",
                schema: "video",
                table: "Videos",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "MimeType",
                schema: "video",
                table: "Videos",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Genres",
                schema: "video",
                table: "Videos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                schema: "video",
                table: "Videos",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ExternalPosterPath",
                schema: "video",
                table: "Videos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "Videos",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "ContentHash",
                schema: "video",
                table: "Videos",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "VideoCodec",
                schema: "video",
                table: "VideoMetadata",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExtractedAt",
                schema: "video",
                table: "VideoMetadata",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "ContainerFormat",
                schema: "video",
                table: "VideoMetadata",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AudioCodec",
                schema: "video",
                table: "VideoMetadata",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "video",
                table: "VideoEpisodes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Overview",
                schema: "video",
                table: "VideoEpisodes",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "AddedAt",
                schema: "video",
                table: "VideoEpisodes",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                schema: "video",
                table: "VideoCollections",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "video",
                table: "VideoCollections",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "video",
                table: "VideoCollections",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "VideoCollections",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AddedAt",
                schema: "video",
                table: "VideoCollectionItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "Language",
                schema: "video",
                table: "Subtitles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                schema: "video",
                table: "Subtitles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Format",
                schema: "video",
                table: "Subtitles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                schema: "video",
                table: "Subtitles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<int>(
                name: "TmdbId",
                schema: "video",
                table: "canonical_tmdb_data",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

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
                name: "uq_video_series_items_series_video",
                schema: "video",
                table: "VideoSeriesItems",
                columns: new[] { "SeriesId", "VideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_video_series_name",
                schema: "video",
                table: "VideoSeries",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "ix_video_series_owner_id",
                schema: "video",
                table: "VideoSeries",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_video_series_tmdb_id",
                schema: "video",
                table: "VideoSeries",
                column: "TmdbId");

            migrationBuilder.CreateIndex(
                name: "uq_video_seasons_series_season_number",
                schema: "video",
                table: "VideoSeasons",
                columns: new[] { "SeriesId", "SeasonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_videos_content_hash",
                schema: "video",
                table: "Videos",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_videos_is_deleted",
                schema: "video",
                table: "Videos",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_videos_last_enriched_at",
                schema: "video",
                table: "Videos",
                column: "LastEnrichedAt");

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
                name: "ix_videos_tmdb_id",
                schema: "video",
                table: "Videos",
                column: "TmdbId");

            migrationBuilder.CreateIndex(
                name: "uq_videos_file_node_owner_id",
                schema: "video",
                table: "Videos",
                columns: new[] { "FileNodeId", "OwnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_video_episodes_season_video",
                schema: "video",
                table: "VideoEpisodes",
                columns: new[] { "SeasonId", "VideoId" },
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
                name: "uq_collection_items_collection_video",
                schema: "video",
                table: "VideoCollectionItems",
                columns: new[] { "CollectionId", "VideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subtitles_video_language",
                schema: "video",
                table: "Subtitles",
                columns: new[] { "VideoId", "Language" });
        }
    }
}
