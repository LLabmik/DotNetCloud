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
                name: "canonical_albums",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    HasCoverArt = table.Column<bool>(type: "boolean", nullable: false),
                    CoverArtHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TotalDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    MusicBrainzReleaseGroupId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    MusicBrainzReleaseId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    LastEnrichedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_albums", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "canonical_artists",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MusicBrainzId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    Biography = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    WikipediaUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DiscogsUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OfficialUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LastEnrichedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_artists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "canonical_genres",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_genres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "canonical_tracks",
                schema: "music",
                columns: table => new
                {
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TrackNumber = table.Column<int>(type: "integer", nullable: true),
                    DiscNumber = table.Column<int>(type: "integer", nullable: true),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    Bitrate = table.Column<long>(type: "bigint", nullable: true),
                    SampleRate = table.Column<int>(type: "integer", nullable: true),
                    Channels = table.Column<int>(type: "integer", nullable: true),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: true),
                    MusicBrainzRecordingId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: true),
                    Isrc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Bpm = table.Column<int>(type: "integer", nullable: true),
                    Composers = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_tracks", x => x.ContentHash);
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
                name: "user_albums",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalAlbumId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_albums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_albums_canonical_albums_CanonicalAlbumId",
                        column: x => x.CanonicalAlbumId,
                        principalSchema: "music",
                        principalTable: "canonical_albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "canonical_album_artists",
                schema: "music",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_album_artists", x => new { x.AlbumId, x.ArtistId });
                    table.ForeignKey(
                        name: "FK_canonical_album_artists_canonical_albums_AlbumId",
                        column: x => x.AlbumId,
                        principalSchema: "music",
                        principalTable: "canonical_albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_canonical_album_artists_canonical_artists_ArtistId",
                        column: x => x.ArtistId,
                        principalSchema: "music",
                        principalTable: "canonical_artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_artists",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_artists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_artists_canonical_artists_CanonicalArtistId",
                        column: x => x.CanonicalArtistId,
                        principalSchema: "music",
                        principalTable: "canonical_artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "canonical_track_artists",
                schema: "music",
                columns: table => new
                {
                    TrackContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_track_artists", x => new { x.TrackContentHash, x.ArtistId });
                    table.ForeignKey(
                        name: "FK_canonical_track_artists_canonical_artists_ArtistId",
                        column: x => x.ArtistId,
                        principalSchema: "music",
                        principalTable: "canonical_artists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_canonical_track_artists_canonical_tracks_TrackContentHash",
                        column: x => x.TrackContentHash,
                        principalSchema: "music",
                        principalTable: "canonical_tracks",
                        principalColumn: "ContentHash",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "canonical_track_genres",
                schema: "music",
                columns: table => new
                {
                    TrackContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GenreId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_track_genres", x => new { x.TrackContentHash, x.GenreId });
                    table.ForeignKey(
                        name: "FK_canonical_track_genres_canonical_genres_GenreId",
                        column: x => x.GenreId,
                        principalSchema: "music",
                        principalTable: "canonical_genres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_canonical_track_genres_canonical_tracks_TrackContentHash",
                        column: x => x.TrackContentHash,
                        principalSchema: "music",
                        principalTable: "canonical_tracks",
                        principalColumn: "ContentHash",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tracks",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalTrackHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CanonicalAlbumId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlayCount = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_tracks_canonical_albums_CanonicalAlbumId",
                        column: x => x.CanonicalAlbumId,
                        principalSchema: "music",
                        principalTable: "canonical_albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_tracks_canonical_tracks_CanonicalTrackHash",
                        column: x => x.CanonicalTrackHash,
                        principalSchema: "music",
                        principalTable: "canonical_tracks",
                        principalColumn: "ContentHash",
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
                name: "PlaybackHistories",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserTrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DurationPlayedSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybackHistories_user_tracks_UserTrackId",
                        column: x => x.UserTrackId,
                        principalSchema: "music",
                        principalTable: "user_tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistTracks",
                schema: "music",
                columns: table => new
                {
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserTrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistTracks", x => new { x.PlaylistId, x.UserTrackId });
                    table.ForeignKey(
                        name: "FK_PlaylistTracks_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalSchema: "music",
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaylistTracks_user_tracks_UserTrackId",
                        column: x => x.UserTrackId,
                        principalSchema: "music",
                        principalTable: "user_tracks",
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
                    UserTrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtistName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TrackTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AlbumTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ScrobbledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrobbleRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScrobbleRecords_user_tracks_UserTrackId",
                        column: x => x.UserTrackId,
                        principalSchema: "music",
                        principalTable: "user_tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_canonical_album_artists_album_primary",
                schema: "music",
                table: "canonical_album_artists",
                columns: new[] { "AlbumId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "ix_canonical_album_artists_artist_id",
                schema: "music",
                table: "canonical_album_artists",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_albums_musicbrainz_release_group_id",
                schema: "music",
                table: "canonical_albums",
                column: "MusicBrainzReleaseGroupId");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_albums_title",
                schema: "music",
                table: "canonical_albums",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_albums_year",
                schema: "music",
                table: "canonical_albums",
                column: "Year");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_artists_name",
                schema: "music",
                table: "canonical_artists",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_artists_name_mbid",
                schema: "music",
                table: "canonical_artists",
                columns: new[] { "Name", "MusicBrainzId" });

            migrationBuilder.CreateIndex(
                name: "uq_canonical_artists_musicbrainz_id",
                schema: "music",
                table: "canonical_artists",
                column: "MusicBrainzId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_canonical_genres_name",
                schema: "music",
                table: "canonical_genres",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_canonical_track_artists_artist_id",
                schema: "music",
                table: "canonical_track_artists",
                column: "ArtistId");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_track_artists_track_primary",
                schema: "music",
                table: "canonical_track_artists",
                columns: new[] { "TrackContentHash", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "ix_canonical_track_genres_genre_id",
                schema: "music",
                table: "canonical_track_genres",
                column: "GenreId");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_tracks_isrc",
                schema: "music",
                table: "canonical_tracks",
                column: "Isrc");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_tracks_musicbrainz_recording_id",
                schema: "music",
                table: "canonical_tracks",
                column: "MusicBrainzRecordingId");

            migrationBuilder.CreateIndex(
                name: "ix_canonical_tracks_title",
                schema: "music",
                table: "canonical_tracks",
                column: "Title");

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
                name: "ix_playback_history_user_played_at",
                schema: "music",
                table: "PlaybackHistories",
                columns: new[] { "UserId", "PlayedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_playback_history_user_track_id",
                schema: "music",
                table: "PlaybackHistories",
                column: "UserTrackId");

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
                name: "IX_PlaylistTracks_UserTrackId",
                schema: "music",
                table: "PlaylistTracks",
                column: "UserTrackId");

            migrationBuilder.CreateIndex(
                name: "ix_scrobble_records_user_scrobbled_at",
                schema: "music",
                table: "ScrobbleRecords",
                columns: new[] { "UserId", "ScrobbledAt" });

            migrationBuilder.CreateIndex(
                name: "ix_scrobble_records_user_track_id",
                schema: "music",
                table: "ScrobbleRecords",
                column: "UserTrackId");

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
                name: "ix_user_albums_canonical_album_id",
                schema: "music",
                table: "user_albums",
                column: "CanonicalAlbumId");

            migrationBuilder.CreateIndex(
                name: "ix_user_albums_is_deleted",
                schema: "music",
                table: "user_albums",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_user_albums_owner_id",
                schema: "music",
                table: "user_albums",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "uq_user_albums_owner_album",
                schema: "music",
                table: "user_albums",
                columns: new[] { "OwnerId", "CanonicalAlbumId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_artists_canonical_artist_id",
                schema: "music",
                table: "user_artists",
                column: "CanonicalArtistId");

            migrationBuilder.CreateIndex(
                name: "ix_user_artists_is_deleted",
                schema: "music",
                table: "user_artists",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_user_artists_owner_id",
                schema: "music",
                table: "user_artists",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "uq_user_artists_owner_artist",
                schema: "music",
                table: "user_artists",
                columns: new[] { "OwnerId", "CanonicalArtistId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_tracks_canonical_track_hash",
                schema: "music",
                table: "user_tracks",
                column: "CanonicalTrackHash");

            migrationBuilder.CreateIndex(
                name: "IX_user_tracks_CanonicalAlbumId",
                schema: "music",
                table: "user_tracks",
                column: "CanonicalAlbumId");

            migrationBuilder.CreateIndex(
                name: "ix_user_tracks_content_hash",
                schema: "music",
                table: "user_tracks",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_user_tracks_is_deleted",
                schema: "music",
                table: "user_tracks",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_user_tracks_owner_created_at",
                schema: "music",
                table: "user_tracks",
                columns: new[] { "OwnerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_user_tracks_owner_id",
                schema: "music",
                table: "user_tracks",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "uq_user_tracks_file_node_owner_id",
                schema: "music",
                table: "user_tracks",
                columns: new[] { "FileNodeId", "OwnerId" },
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
                name: "canonical_album_artists",
                schema: "music");

            migrationBuilder.DropTable(
                name: "canonical_track_artists",
                schema: "music");

            migrationBuilder.DropTable(
                name: "canonical_track_genres",
                schema: "music");

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
                name: "user_albums",
                schema: "music");

            migrationBuilder.DropTable(
                name: "user_artists",
                schema: "music");

            migrationBuilder.DropTable(
                name: "UserMusicPreferences",
                schema: "music");

            migrationBuilder.DropTable(
                name: "canonical_genres",
                schema: "music");

            migrationBuilder.DropTable(
                name: "Playlists",
                schema: "music");

            migrationBuilder.DropTable(
                name: "user_tracks",
                schema: "music");

            migrationBuilder.DropTable(
                name: "canonical_artists",
                schema: "music");

            migrationBuilder.DropTable(
                name: "EqPresets",
                schema: "music");

            migrationBuilder.DropTable(
                name: "canonical_albums",
                schema: "music");

            migrationBuilder.DropTable(
                name: "canonical_tracks",
                schema: "music");
        }
    }
}
