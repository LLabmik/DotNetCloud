using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Music.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyDualWriteTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackHistories_Tracks_TrackId",
                schema: "music",
                table: "PlaybackHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaylistTracks_Tracks_TrackId",
                schema: "music",
                table: "PlaylistTracks");

            migrationBuilder.DropForeignKey(
                name: "FK_ScrobbleRecords_Tracks_TrackId",
                schema: "music",
                table: "ScrobbleRecords");

            migrationBuilder.DropTable(
                name: "TrackArtists",
                schema: "music");

            migrationBuilder.DropTable(
                name: "TrackGenres",
                schema: "music");

            migrationBuilder.DropTable(
                name: "Genres",
                schema: "music");

            migrationBuilder.DropTable(
                name: "Tracks",
                schema: "music");

            migrationBuilder.DropTable(
                name: "Albums",
                schema: "music");

            migrationBuilder.DropTable(
                name: "Artists",
                schema: "music");

            migrationBuilder.RenameColumn(
                name: "TrackId",
                schema: "music",
                table: "ScrobbleRecords",
                newName: "UserTrackId");

            migrationBuilder.RenameIndex(
                name: "ix_scrobble_records_track_id",
                schema: "music",
                table: "ScrobbleRecords",
                newName: "ix_scrobble_records_user_track_id");

            migrationBuilder.RenameColumn(
                name: "TrackId",
                schema: "music",
                table: "PlaylistTracks",
                newName: "UserTrackId");

            migrationBuilder.RenameIndex(
                name: "IX_PlaylistTracks_TrackId",
                schema: "music",
                table: "PlaylistTracks",
                newName: "IX_PlaylistTracks_UserTrackId");

            migrationBuilder.RenameColumn(
                name: "TrackId",
                schema: "music",
                table: "PlaybackHistories",
                newName: "UserTrackId");

            migrationBuilder.RenameIndex(
                name: "ix_playback_history_track_id",
                schema: "music",
                table: "PlaybackHistories",
                newName: "ix_playback_history_user_track_id");

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackHistories_user_tracks_UserTrackId",
                schema: "music",
                table: "PlaybackHistories",
                column: "UserTrackId",
                principalSchema: "music",
                principalTable: "user_tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaylistTracks_user_tracks_UserTrackId",
                schema: "music",
                table: "PlaylistTracks",
                column: "UserTrackId",
                principalSchema: "music",
                principalTable: "user_tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScrobbleRecords_user_tracks_UserTrackId",
                schema: "music",
                table: "ScrobbleRecords",
                column: "UserTrackId",
                principalSchema: "music",
                principalTable: "user_tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackHistories_user_tracks_UserTrackId",
                schema: "music",
                table: "PlaybackHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_PlaylistTracks_user_tracks_UserTrackId",
                schema: "music",
                table: "PlaylistTracks");

            migrationBuilder.DropForeignKey(
                name: "FK_ScrobbleRecords_user_tracks_UserTrackId",
                schema: "music",
                table: "ScrobbleRecords");

            migrationBuilder.RenameColumn(
                name: "UserTrackId",
                schema: "music",
                table: "ScrobbleRecords",
                newName: "TrackId");

            migrationBuilder.RenameIndex(
                name: "ix_scrobble_records_user_track_id",
                schema: "music",
                table: "ScrobbleRecords",
                newName: "ix_scrobble_records_track_id");

            migrationBuilder.RenameColumn(
                name: "UserTrackId",
                schema: "music",
                table: "PlaylistTracks",
                newName: "TrackId");

            migrationBuilder.RenameIndex(
                name: "IX_PlaylistTracks_UserTrackId",
                schema: "music",
                table: "PlaylistTracks",
                newName: "IX_PlaylistTracks_TrackId");

            migrationBuilder.RenameColumn(
                name: "UserTrackId",
                schema: "music",
                table: "PlaybackHistories",
                newName: "TrackId");

            migrationBuilder.RenameIndex(
                name: "ix_playback_history_user_track_id",
                schema: "music",
                table: "PlaybackHistories",
                newName: "ix_playback_history_track_id");

            migrationBuilder.CreateTable(
                name: "Artists",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Biography = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiscogsUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastEnrichedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MusicBrainzId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OfficialUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    WikipediaUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Genres",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Albums",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoverArtPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HasCoverArt = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastEnrichedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MusicBrainzReleaseGroupId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    MusicBrainzReleaseId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TotalDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Year = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Albums_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalSchema: "music",
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tracks",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AlbumId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Bitrate = table.Column<long>(type: "bigint", nullable: true),
                    Channels = table.Column<int>(type: "int", nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DiscNumber = table.Column<int>(type: "int", nullable: true),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LastEnrichedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MusicBrainzRecordingId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayCount = table.Column<int>(type: "int", nullable: false),
                    SampleRate = table.Column<int>(type: "int", nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TrackNumber = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Year = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tracks_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalSchema: "music",
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TrackArtists",
                schema: "music",
                columns: table => new
                {
                    TrackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackArtists", x => new { x.TrackId, x.ArtistId });
                    table.ForeignKey(
                        name: "FK_TrackArtists_Artists_ArtistId",
                        column: x => x.ArtistId,
                        principalSchema: "music",
                        principalTable: "Artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrackArtists_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalSchema: "music",
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackGenres",
                schema: "music",
                columns: table => new
                {
                    TrackId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GenreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackGenres", x => new { x.TrackId, x.GenreId });
                    table.ForeignKey(
                        name: "FK_TrackGenres_Genres_GenreId",
                        column: x => x.GenreId,
                        principalSchema: "music",
                        principalTable: "Genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrackGenres_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalSchema: "music",
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_music_albums_artist_id",
                schema: "music",
                table: "Albums",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "ix_music_albums_is_deleted",
                schema: "music",
                table: "Albums",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_music_albums_musicbrainz_release_group_id",
                schema: "music",
                table: "Albums",
                column: "MusicBrainzReleaseGroupId");

            migrationBuilder.CreateIndex(
                name: "ix_music_albums_owner_id",
                schema: "music",
                table: "Albums",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_music_albums_title",
                schema: "music",
                table: "Albums",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "ix_music_albums_year",
                schema: "music",
                table: "Albums",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "ix_artists_is_deleted",
                schema: "music",
                table: "Artists",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_artists_musicbrainz_id",
                schema: "music",
                table: "Artists",
                column: "MusicBrainzId");

            migrationBuilder.CreateIndex(
                name: "ix_artists_name",
                schema: "music",
                table: "Artists",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "ix_artists_owner_id",
                schema: "music",
                table: "Artists",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "uq_artists_owner_name",
                schema: "music",
                table: "Artists",
                columns: new[] { "OwnerId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_genres_name",
                schema: "music",
                table: "Genres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_track_artists_artist_id",
                schema: "music",
                table: "TrackArtists",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "ix_track_artists_track_primary",
                schema: "music",
                table: "TrackArtists",
                columns: new[] { "TrackId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "ix_track_genres_genre_id",
                schema: "music",
                table: "TrackGenres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_album_id",
                schema: "music",
                table: "Tracks",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_content_hash",
                schema: "music",
                table: "Tracks",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_is_deleted",
                schema: "music",
                table: "Tracks",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_musicbrainz_recording_id",
                schema: "music",
                table: "Tracks",
                column: "MusicBrainzRecordingId");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_owner_created_at",
                schema: "music",
                table: "Tracks",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_tracks_owner_id",
                schema: "music",
                table: "Tracks",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_tracks_title",
                schema: "music",
                table: "Tracks",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "uq_tracks_file_node_owner_id",
                schema: "music",
                table: "Tracks",
                columns: new[] { "FileNodeId", "OwnerId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackHistories_Tracks_TrackId",
                schema: "music",
                table: "PlaybackHistories",
                column: "TrackId",
                principalSchema: "music",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlaylistTracks_Tracks_TrackId",
                schema: "music",
                table: "PlaylistTracks",
                column: "TrackId",
                principalSchema: "music",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ScrobbleRecords_Tracks_TrackId",
                schema: "music",
                table: "ScrobbleRecords",
                column: "TrackId",
                principalSchema: "music",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
