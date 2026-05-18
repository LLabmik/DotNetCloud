using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Notes.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notes");

            migrationBuilder.CreateTable(
                name: "NoteFolders",
                schema: "notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteFolders_NoteFolders_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "notes",
                        principalTable: "NoteFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                schema: "notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    IsFavorite = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ETag = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notes_NoteFolders_FolderId",
                        column: x => x.FolderId,
                        principalSchema: "notes",
                        principalTable: "NoteFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NoteLinks",
                schema: "notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LinkType = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteLinks_Notes_NoteId",
                        column: x => x.NoteId,
                        principalSchema: "notes",
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoteShares",
                schema: "notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedWithUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permission = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteShares_Notes_NoteId",
                        column: x => x.NoteId,
                        principalSchema: "notes",
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoteTags",
                schema: "notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tag = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteTags_Notes_NoteId",
                        column: x => x.NoteId,
                        principalSchema: "notes",
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NoteVersions",
                schema: "notes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    EditedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NoteVersions_Notes_NoteId",
                        column: x => x.NoteId,
                        principalSchema: "notes",
                        principalTable: "Notes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_note_folders_owner_id",
                schema: "notes",
                table: "NoteFolders",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_note_folders_owner_name",
                schema: "notes",
                table: "NoteFolders",
                columns: new[] { "OwnerId", "Name" });

            migrationBuilder.CreateIndex(
                name: "ix_note_folders_owner_parent",
                schema: "notes",
                table: "NoteFolders",
                columns: new[] { "OwnerId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteFolders_ParentId",
                schema: "notes",
                table: "NoteFolders",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "ix_note_links_note_target",
                schema: "notes",
                table: "NoteLinks",
                columns: new[] { "NoteId", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "ix_note_links_target_id",
                schema: "notes",
                table: "NoteLinks",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_FolderId",
                schema: "notes",
                table: "Notes",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "ix_notes_is_deleted",
                schema: "notes",
                table: "Notes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_notes_is_pinned",
                schema: "notes",
                table: "Notes",
                column: "IsPinned");

            migrationBuilder.CreateIndex(
                name: "ix_notes_owner_folder",
                schema: "notes",
                table: "Notes",
                columns: new[] { "OwnerId", "FolderId" });

            migrationBuilder.CreateIndex(
                name: "ix_notes_owner_id",
                schema: "notes",
                table: "Notes",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_note_shares_note_user",
                schema: "notes",
                table: "NoteShares",
                columns: new[] { "NoteId", "SharedWithUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_note_shares_user_id",
                schema: "notes",
                table: "NoteShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "ix_note_tags_note_tag",
                schema: "notes",
                table: "NoteTags",
                columns: new[] { "NoteId", "Tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_note_tags_tag",
                schema: "notes",
                table: "NoteTags",
                column: "Tag");

            migrationBuilder.CreateIndex(
                name: "ix_note_versions_note_id",
                schema: "notes",
                table: "NoteVersions",
                column: "NoteId");

            migrationBuilder.CreateIndex(
                name: "ix_note_versions_note_version",
                schema: "notes",
                table: "NoteVersions",
                columns: new[] { "NoteId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoteLinks",
                schema: "notes");

            migrationBuilder.DropTable(
                name: "NoteShares",
                schema: "notes");

            migrationBuilder.DropTable(
                name: "NoteTags",
                schema: "notes");

            migrationBuilder.DropTable(
                name: "NoteVersions",
                schema: "notes");

            migrationBuilder.DropTable(
                name: "Notes",
                schema: "notes");

            migrationBuilder.DropTable(
                name: "NoteFolders",
                schema: "notes");
        }
    }
}
