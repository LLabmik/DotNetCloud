using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Video.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoTmdbEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalPosterPath",
                schema: "video",
                table: "Videos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Genres",
                schema: "video",
                table: "Videos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasExternalPoster",
                schema: "video",
                table: "Videos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEnrichedAt",
                schema: "video",
                table: "Videos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Overview",
                schema: "video",
                table: "Videos",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleaseDate",
                schema: "video",
                table: "Videos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TmdbId",
                schema: "video",
                table: "Videos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TmdbRating",
                schema: "video",
                table: "Videos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TmdbTitle",
                schema: "video",
                table: "Videos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_videos_last_enriched_at",
                schema: "video",
                table: "Videos",
                column: "LastEnrichedAt");

            migrationBuilder.CreateIndex(
                name: "ix_videos_tmdb_id",
                schema: "video",
                table: "Videos",
                column: "TmdbId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_videos_last_enriched_at",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropIndex(
                name: "ix_videos_tmdb_id",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "ExternalPosterPath",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Genres",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "HasExternalPoster",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "LastEnrichedAt",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Overview",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "TmdbId",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "TmdbRating",
                schema: "video",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "TmdbTitle",
                schema: "video",
                table: "Videos");
        }
    }
}
