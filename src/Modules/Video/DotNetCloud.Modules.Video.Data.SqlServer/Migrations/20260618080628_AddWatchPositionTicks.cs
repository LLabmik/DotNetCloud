using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Video.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddWatchPositionTicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "watch_position_ticks",
                schema: "video",
                table: "user_videos",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "watch_position_ticks",
                schema: "video",
                table: "user_videos");
        }

        migrationBuilder.CreateTable(
            name: "VideoSeries",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalPosterPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Genres = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HasExternalPoster = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThumbnailPoster = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    TmdbId = table.Column<int>(type: "int", nullable: true),
                    TmdbName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TmdbOverview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TmdbRating = table.Column<double>(type: "float", nullable: true),
                    TotalEpisodes = table.Column<int>(type: "int", nullable: false),
                    TotalSeasons = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoSeries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subtitles",
                schema: "video",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
    name: "VideoMetadata",
    schema: "video",
    columns: table => new
    {
        Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
        VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        AudioCodec = table.Column<string>(type: "nvarchar(max)", nullable: true),
        AudioTrackCount = table.Column<int>(type: "int", nullable: false),
        Bitrate = table.Column<long>(type: "bigint", nullable: false),
        ContainerFormat = table.Column<string>(type: "nvarchar(max)", nullable: true),
        ExtractedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
        FrameRate = table.Column<double>(type: "float", nullable: false),
        Height = table.Column<int>(type: "int", nullable: false),
        SubtitleTrackCount = table.Column<int>(type: "int", nullable: false),
        VideoCodec = table.Column<string>(type: "nvarchar(max)", nullable: true),
        Width = table.Column<int>(type: "int", nullable: false)
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
        Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
        VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
        ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
        Permission = table.Column<string>(type: "nvarchar(max)", nullable: false),
        ShareToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
        SharedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        SharedWithUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
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
        Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
        VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        DurationWatchedSeconds = table.Column<int>(type: "int", nullable: false),
        UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        WatchedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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
        Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
        VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        IsCompleted = table.Column<bool>(type: "bit", nullable: false),
        PositionTicks = table.Column<long>(type: "bigint", nullable: false),
        UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
        UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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

migrationBuilder.CreateTable(
    name: "VideoSeasons",
    schema: "video",
    columns: table => new
    {
        Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
        SeriesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        AirDate = table.Column<DateTime>(type: "datetime2", nullable: true),
        CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
        DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
        EpisodeCount = table.Column<int>(type: "int", nullable: false),
        HasExternalPoster = table.Column<bool>(type: "bit", nullable: false),
        IsDeleted = table.Column<bool>(type: "bit", nullable: false),
        Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
        Overview = table.Column<string>(type: "nvarchar(max)", nullable: true),
        SeasonNumber = table.Column<int>(type: "int", nullable: false),
        ThumbnailPoster = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
        TmdbId = table.Column<int>(type: "int", nullable: true),
        UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
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
        Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
        SeriesId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
        EpisodeTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
        SortOrder = table.Column<int>(type: "int", nullable: false)
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
        Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
        SeasonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        VideoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
        AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
        EpisodeNumber = table.Column<int>(type: "int", nullable: false),
        Overview = table.Column<string>(type: "nvarchar(max)", nullable: true),
        SortOrder = table.Column<int>(type: "int", nullable: false),
        Title = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
    name: "IX_Subtitles_VideoId",
    schema: "video",
    table: "Subtitles",
    column: "VideoId");

migrationBuilder.CreateIndex(
    name: "IX_VideoEpisodes_SeasonId",
    schema: "video",
    table: "VideoEpisodes",
    column: "SeasonId");

migrationBuilder.CreateIndex(
    name: "IX_VideoEpisodes_VideoId",
    schema: "video",
    table: "VideoEpisodes",
    column: "VideoId");

migrationBuilder.CreateIndex(
    name: "IX_VideoMetadata_VideoId",
    schema: "video",
    table: "VideoMetadata",
    column: "VideoId",
    unique: true);

migrationBuilder.CreateIndex(
    name: "IX_VideoSeasons_SeriesId",
    schema: "video",
    table: "VideoSeasons",
    column: "SeriesId");

migrationBuilder.CreateIndex(
    name: "IX_VideoSeriesItems_SeriesId",
    schema: "video",
    table: "VideoSeriesItems",
    column: "SeriesId");

migrationBuilder.CreateIndex(
    name: "IX_VideoSeriesItems_VideoId",
    schema: "video",
    table: "VideoSeriesItems",
    column: "VideoId");

migrationBuilder.CreateIndex(
    name: "IX_VideoShares_VideoId",
    schema: "video",
    table: "VideoShares",
    column: "VideoId");

migrationBuilder.CreateIndex(
    name: "IX_WatchHistories_VideoId",
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
    }
}
