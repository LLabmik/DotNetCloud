using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Music.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMusicBrainzEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastEnrichedAt",
                schema: "music",
                table: "Tracks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicBrainzRecordingId",
                schema: "music",
                table: "Tracks",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Biography",
                schema: "music",
                table: "Artists",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscogsUrl",
                schema: "music",
                table: "Artists",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                schema: "music",
                table: "Artists",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEnrichedAt",
                schema: "music",
                table: "Artists",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicBrainzId",
                schema: "music",
                table: "Artists",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfficialUrl",
                schema: "music",
                table: "Artists",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WikipediaUrl",
                schema: "music",
                table: "Artists",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEnrichedAt",
                schema: "music",
                table: "Albums",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicBrainzReleaseGroupId",
                schema: "music",
                table: "Albums",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MusicBrainzReleaseId",
                schema: "music",
                table: "Albums",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tracks_musicbrainz_recording_id",
                schema: "music",
                table: "Tracks",
                column: "MusicBrainzRecordingId");

            migrationBuilder.CreateIndex(
                name: "ix_artists_musicbrainz_id",
                schema: "music",
                table: "Artists",
                column: "MusicBrainzId");

            migrationBuilder.CreateIndex(
                name: "ix_music_albums_musicbrainz_release_group_id",
                schema: "music",
                table: "Albums",
                column: "MusicBrainzReleaseGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tracks_musicbrainz_recording_id",
                schema: "music",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "ix_artists_musicbrainz_id",
                schema: "music",
                table: "Artists");

            migrationBuilder.DropIndex(
                name: "ix_music_albums_musicbrainz_release_group_id",
                schema: "music",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "LastEnrichedAt",
                schema: "music",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "MusicBrainzRecordingId",
                schema: "music",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "Biography",
                schema: "music",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "DiscogsUrl",
                schema: "music",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                schema: "music",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "LastEnrichedAt",
                schema: "music",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "MusicBrainzId",
                schema: "music",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "OfficialUrl",
                schema: "music",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "WikipediaUrl",
                schema: "music",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "LastEnrichedAt",
                schema: "music",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "MusicBrainzReleaseGroupId",
                schema: "music",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "MusicBrainzReleaseId",
                schema: "music",
                table: "Albums");
        }
    }
}
