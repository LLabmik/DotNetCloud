using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Music.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "canonical_albums",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: true),
                    HasCoverArt = table.Column<bool>(type: "bit", nullable: false),
                    CoverArtHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TotalDurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    MusicBrainzReleaseGroupId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    MusicBrainzReleaseId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    LastEnrichedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SortName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MusicBrainzId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Biography = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    WikipediaUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DiscogsUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OfficialUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LastEnrichedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
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
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TrackNumber = table.Column<int>(type: "int", nullable: true),
                    DiscNumber = table.Column<int>(type: "int", nullable: true),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: false),
                    Bitrate = table.Column<long>(type: "bigint", nullable: true),
                    SampleRate = table.Column<int>(type: "int", nullable: true),
                    Channels = table.Column<int>(type: "int", nullable: true),
                    MimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: true),
                    MusicBrainzRecordingId = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: true),
                    Isrc = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Bpm = table.Column<int>(type: "int", nullable: true),
                    Composers = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_tracks", x => x.ContentHash);
                });

            migrationBuilder.CreateTable(
                name: "user_albums",
                schema: "music",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanonicalAlbumId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
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
                    AlbumId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanonicalArtistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
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
                    TrackContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ArtistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false)
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
                    TrackContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GenreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CanonicalTrackHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CanonicalAlbumId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PlayCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
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
                unique: true,
                filter: "[MusicBrainzId] IS NOT NULL");

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
                name: "user_albums",
                schema: "music");

            migrationBuilder.DropTable(
                name: "user_artists",
                schema: "music");

            migrationBuilder.DropTable(
                name: "user_tracks",
                schema: "music");

            migrationBuilder.DropTable(
                name: "canonical_genres",
                schema: "music");

            migrationBuilder.DropTable(
                name: "canonical_artists",
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
