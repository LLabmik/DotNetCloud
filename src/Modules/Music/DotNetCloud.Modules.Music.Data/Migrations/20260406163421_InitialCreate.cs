using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Music.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "music");

            migrationBuilder.CreateTable(
                name: "Artists",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EqPresets",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false),
                    BandsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EqPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Genres",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Playlists",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StarredItems",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    StarredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StarredItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Albums",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    HasCoverArt = table.Column<bool>(type: "boolean", nullable: false),
                    CoverArtPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TotalDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
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
                name: "UserMusicPreferences",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveEqPresetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Volume = table.Column<double>(type: "double precision", nullable: false),
                    ShuffleEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RepeatMode = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMusicPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMusicPreferences_EqPresets_ActiveEqPresetId",
                        column: x => x.ActiveEqPresetId,
                        principalSchema: "music",
                        principalTable: "EqPresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Tracks",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TrackNumber = table.Column<int>(type: "integer", nullable: true),
                    DiscNumber = table.Column<int>(type: "integer", nullable: true),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Bitrate = table.Column<long>(type: "bigint", nullable: true),
                    SampleRate = table.Column<int>(type: "integer", nullable: true),
                    Channels = table.Column<int>(type: "integer", nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    PlayCount = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
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
                name: "PlaybackHistories",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DurationPlayedSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybackHistories_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalSchema: "music",
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistTracks",
                schema: "music",
                columns: table => new
                {
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistTracks", x => new { x.PlaylistId, x.TrackId });
                    table.ForeignKey(
                        name: "FK_PlaylistTracks_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalSchema: "music",
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaylistTracks_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalSchema: "music",
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScrobbleRecords",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtistName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TrackTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AlbumTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ScrobbledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrobbleRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrobbleRecords_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalSchema: "music",
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackArtists",
                schema: "music",
                columns: table => new
                {
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
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
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    GenreId = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "ix_eq_presets_owner_id",
                schema: "music",
                table: "EqPresets",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_eq_presets_owner_name",
                schema: "music",
                table: "EqPresets",
                columns: new[] { "OwnerId", "Name" });

            migrationBuilder.CreateIndex(
                name: "uq_genres_name",
                schema: "music",
                table: "Genres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_playback_history_track_id",
                schema: "music",
                table: "PlaybackHistories",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "ix_playback_history_user_played_at",
                schema: "music",
                table: "PlaybackHistories",
                columns: new[] { "UserId", "PlayedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_playlists_is_deleted",
                schema: "music",
                table: "Playlists",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_playlists_name",
                schema: "music",
                table: "Playlists",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "ix_playlists_owner_id",
                schema: "music",
                table: "Playlists",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_tracks_playlist_sort",
                schema: "music",
                table: "PlaylistTracks",
                columns: new[] { "PlaylistId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistTracks_TrackId",
                schema: "music",
                table: "PlaylistTracks",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "ix_scrobble_records_track_id",
                schema: "music",
                table: "ScrobbleRecords",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "ix_scrobble_records_user_scrobbled_at",
                schema: "music",
                table: "ScrobbleRecords",
                columns: new[] { "UserId", "ScrobbledAt" });

            migrationBuilder.CreateIndex(
                name: "ix_starred_items_user_id",
                schema: "music",
                table: "StarredItems",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "uq_starred_items_user_type_item",
                schema: "music",
                table: "StarredItems",
                columns: new[] { "UserId", "ItemType", "ItemId" },
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
                name: "ix_tracks_is_deleted",
                schema: "music",
                table: "Tracks",
                column: "IsDeleted");

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
                name: "uq_tracks_file_node_id",
                schema: "music",
                table: "Tracks",
                column: "FileNodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserMusicPreferences_ActiveEqPresetId",
                schema: "music",
                table: "UserMusicPreferences",
                column: "ActiveEqPresetId");

            migrationBuilder.CreateIndex(
                name: "uq_user_music_preferences_user_id",
                schema: "music",
                table: "UserMusicPreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaybackHistories",
                schema: "music");

            migrationBuilder.DropTable(
                name: "PlaylistTracks",
                schema: "music");

            migrationBuilder.DropTable(
                name: "ScrobbleRecords",
                schema: "music");

            migrationBuilder.DropTable(
                name: "StarredItems",
                schema: "music");

            migrationBuilder.DropTable(
                name: "TrackArtists",
                schema: "music");

            migrationBuilder.DropTable(
                name: "TrackGenres",
                schema: "music");

            migrationBuilder.DropTable(
                name: "UserMusicPreferences",
                schema: "music");

            migrationBuilder.DropTable(
                name: "Playlists",
                schema: "music");

            migrationBuilder.DropTable(
                name: "Genres",
                schema: "music");

            migrationBuilder.DropTable(
                name: "Tracks",
                schema: "music");

            migrationBuilder.DropTable(
                name: "EqPresets",
                schema: "music");

            migrationBuilder.DropTable(
                name: "Albums",
                schema: "music");

            migrationBuilder.DropTable(
                name: "Artists",
                schema: "music");
        }
    }
}
