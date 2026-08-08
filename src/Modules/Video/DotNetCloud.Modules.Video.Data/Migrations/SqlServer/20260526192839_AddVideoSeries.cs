using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Video.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoSeries",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ThumbnailPoster = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    HasExternalPoster = table.Column<bool>(type: "bit", nullable: false),
                    ExternalPosterPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TmdbId = table.Column<int>(type: "int", nullable: true),
                    TmdbName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TmdbOverview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TmdbRating = table.Column<double>(type: "float", nullable: true),
                    Genres = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Year = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TotalSeasons = table.Column<int>(type: "int", nullable: false),
                    TotalEpisodes = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoSeries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoSeasons",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Overview = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ThumbnailPoster = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    HasExternalPoster = table.Column<bool>(type: "bit", nullable: false),
                    TmdbId = table.Column<int>(type: "int", nullable: true),
                    EpisodeCount = table.Column<int>(type: "int", nullable: false),
                    AirDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoSeasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoSeasons_VideoSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalSchema: "video",
                        principalTable: "VideoSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoSeriesItems",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    EpisodeTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoSeriesItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoSeriesItems_VideoSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalSchema: "video",
                        principalTable: "VideoSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoSeriesItems_Videos_VideoId",
                        column: x => x.VideoId,
                        principalSchema: "video",
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoEpisodes",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Overview = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoEpisodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoEpisodes_VideoSeasons_SeasonId",
                        column: x => x.SeasonId,
                        principalSchema: "video",
                        principalTable: "VideoSeasons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VideoEpisodes_Videos_VideoId",
                        column: x => x.VideoId,
                        principalSchema: "video",
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_video_episodes_season_id",
                schema: "video",
                table: "VideoEpisodes",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "ix_video_episodes_video_id",
                schema: "video",
                table: "VideoEpisodes",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "uq_video_episodes_season_video",
                schema: "video",
                table: "VideoEpisodes",
                columns: new[] { "SeasonId", "VideoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_video_seasons_series_id",
                schema: "video",
                table: "VideoSeasons",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "uq_video_seasons_series_season_number",
                schema: "video",
                table: "VideoSeasons",
                columns: new[] { "SeriesId", "SeasonNumber" },
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
                name: "ix_video_series_items_series_id",
                schema: "video",
                table: "VideoSeriesItems",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "ix_video_series_items_video_id",
                schema: "video",
                table: "VideoSeriesItems",
                column: "VideoId");

            migrationBuilder.CreateIndex(
                name: "uq_video_series_items_series_video",
                schema: "video",
                table: "VideoSeriesItems",
                columns: new[] { "SeriesId", "VideoId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoEpisodes",
                schema: "video");

            migrationBuilder.DropTable(
                name: "VideoSeriesItems",
                schema: "video");

            migrationBuilder.DropTable(
                name: "VideoSeasons",
                schema: "video");

            migrationBuilder.DropTable(
                name: "VideoSeries",
                schema: "video");
        }
    }
}
