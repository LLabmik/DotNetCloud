using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Photos.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "photos");

            migrationBuilder.CreateTable(
                name: "Albums",
                schema: "photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CoverPhotoId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Photos",
                schema: "photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    TakenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Photos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlbumPhotos",
                schema: "photos",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumPhotos", x => new { x.AlbumId, x.PhotoId });
                    table.ForeignKey(
                        name: "FK_AlbumPhotos_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalSchema: "photos",
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumPhotos_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalSchema: "photos",
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhotoEditRecords",
                schema: "photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ParametersJson = table.Column<string>(type: "text", nullable: false),
                    StackOrder = table.Column<int>(type: "integer", nullable: false),
                    EditedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoEditRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoEditRecords_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalSchema: "photos",
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhotoMetadata",
                schema: "photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    CameraMake = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CameraModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LensModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FocalLengthMm = table.Column<double>(type: "double precision", nullable: true),
                    Aperture = table.Column<double>(type: "double precision", nullable: true),
                    ShutterSpeed = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Iso = table.Column<int>(type: "integer", nullable: true),
                    FlashFired = table.Column<bool>(type: "boolean", nullable: true),
                    Orientation = table.Column<int>(type: "integer", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    AltitudeMetres = table.Column<double>(type: "double precision", nullable: true),
                    TakenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoMetadata", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoMetadata_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalSchema: "photos",
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhotoShares",
                schema: "photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: true),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: true),
                    SharedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedWithUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Permission = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoShares_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalSchema: "photos",
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhotoShares_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalSchema: "photos",
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhotoTags",
                schema: "photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tag = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoTags_Photos_PhotoId",
                        column: x => x.PhotoId,
                        principalSchema: "photos",
                        principalTable: "Photos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_album_photos_album_sort",
                schema: "photos",
                table: "AlbumPhotos",
                columns: new[] { "AlbumId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumPhotos_PhotoId",
                schema: "photos",
                table: "AlbumPhotos",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "ix_albums_created_at",
                schema: "photos",
                table: "Albums",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_albums_is_deleted",
                schema: "photos",
                table: "Albums",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_albums_owner_id",
                schema: "photos",
                table: "Albums",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_photo_edit_records_photo_order",
                schema: "photos",
                table: "PhotoEditRecords",
                columns: new[] { "PhotoId", "StackOrder" });

            migrationBuilder.CreateIndex(
                name: "ix_photo_metadata_geo",
                schema: "photos",
                table: "PhotoMetadata",
                columns: new[] { "Latitude", "Longitude" },
                filter: "\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoMetadata_PhotoId",
                schema: "photos",
                table: "PhotoMetadata",
                column: "PhotoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_photos_created_at",
                schema: "photos",
                table: "Photos",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_photos_is_deleted",
                schema: "photos",
                table: "Photos",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_photos_owner_favorite",
                schema: "photos",
                table: "Photos",
                columns: new[] { "OwnerId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "ix_photos_owner_id",
                schema: "photos",
                table: "Photos",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_photos_owner_taken_at",
                schema: "photos",
                table: "Photos",
                columns: new[] { "OwnerId", "TakenAt" });

            migrationBuilder.CreateIndex(
                name: "uq_photos_file_node_id",
                schema: "photos",
                table: "Photos",
                column: "FileNodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_photo_shares_album_id",
                schema: "photos",
                table: "PhotoShares",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "ix_photo_shares_photo_id",
                schema: "photos",
                table: "PhotoShares",
                column: "PhotoId");

            migrationBuilder.CreateIndex(
                name: "ix_photo_shares_shared_with",
                schema: "photos",
                table: "PhotoShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "ix_photo_tags_tag",
                schema: "photos",
                table: "PhotoTags",
                column: "Tag");

            migrationBuilder.CreateIndex(
                name: "uq_photo_tags_photo_tag",
                schema: "photos",
                table: "PhotoTags",
                columns: new[] { "PhotoId", "Tag" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlbumPhotos",
                schema: "photos");

            migrationBuilder.DropTable(
                name: "PhotoEditRecords",
                schema: "photos");

            migrationBuilder.DropTable(
                name: "PhotoMetadata",
                schema: "photos");

            migrationBuilder.DropTable(
                name: "PhotoShares",
                schema: "photos");

            migrationBuilder.DropTable(
                name: "PhotoTags",
                schema: "photos");

            migrationBuilder.DropTable(
                name: "Albums",
                schema: "photos");

            migrationBuilder.DropTable(
                name: "Photos",
                schema: "photos");
        }
    }
}
