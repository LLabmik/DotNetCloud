using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Bookmarks.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bookmarks");

            migrationBuilder.CreateTable(
                name: "BookmarkFolders",
                schema: "bookmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookmarkFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookmarkFolders_BookmarkFolders_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "bookmarks",
                        principalTable: "BookmarkFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Bookmarks",
                schema: "bookmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    NormalizedUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookmarks_BookmarkFolders_FolderId",
                        column: x => x.FolderId,
                        principalSchema: "bookmarks",
                        principalTable: "BookmarkFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BookmarkPreviews",
                schema: "bookmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookmarkId = table.Column<Guid>(type: "uuid", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CanonicalUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SiteName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResolvedTitle = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResolvedDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FaviconUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    PreviewImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContentLength = table.Column<long>(type: "bigint", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ETag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastModified = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookmarkPreviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookmarkPreviews_Bookmarks_BookmarkId",
                        column: x => x.BookmarkId,
                        principalSchema: "bookmarks",
                        principalTable: "Bookmarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookmark_folders_owner_id",
                schema: "bookmarks",
                table: "BookmarkFolders",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_bookmark_folders_owner_parent",
                schema: "bookmarks",
                table: "BookmarkFolders",
                columns: new[] { "OwnerId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_BookmarkFolders_ParentId",
                schema: "bookmarks",
                table: "BookmarkFolders",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "ix_bookmark_previews_bookmark_id",
                schema: "bookmarks",
                table: "BookmarkPreviews",
                column: "BookmarkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookmark_previews_status",
                schema: "bookmarks",
                table: "BookmarkPreviews",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_FolderId",
                schema: "bookmarks",
                table: "Bookmarks",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "ix_bookmarks_normalized_url",
                schema: "bookmarks",
                table: "Bookmarks",
                column: "NormalizedUrl");

            migrationBuilder.CreateIndex(
                name: "ix_bookmarks_owner_folder",
                schema: "bookmarks",
                table: "Bookmarks",
                columns: new[] { "OwnerId", "FolderId" });

            migrationBuilder.CreateIndex(
                name: "ix_bookmarks_owner_id",
                schema: "bookmarks",
                table: "Bookmarks",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookmarkPreviews",
                schema: "bookmarks");

            migrationBuilder.DropTable(
                name: "Bookmarks",
                schema: "bookmarks");

            migrationBuilder.DropTable(
                name: "BookmarkFolders",
                schema: "bookmarks");
        }
    }
}
