using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Notes.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notes");

            migrationBuilder.RenameTable(
                name: "NoteVersions",
                newName: "NoteVersions",
                newSchema: "notes");

            migrationBuilder.RenameTable(
                name: "NoteTags",
                newName: "NoteTags",
                newSchema: "notes");

            migrationBuilder.RenameTable(
                name: "NoteShares",
                newName: "NoteShares",
                newSchema: "notes");

            migrationBuilder.RenameTable(
                name: "Notes",
                newName: "Notes",
                newSchema: "notes");

            migrationBuilder.RenameTable(
                name: "NoteLinks",
                newName: "NoteLinks",
                newSchema: "notes");

            migrationBuilder.RenameTable(
                name: "NoteFolders",
                newName: "NoteFolders",
                newSchema: "notes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "NoteVersions",
                schema: "notes",
                newName: "NoteVersions");

            migrationBuilder.RenameTable(
                name: "NoteTags",
                schema: "notes",
                newName: "NoteTags");

            migrationBuilder.RenameTable(
                name: "NoteShares",
                schema: "notes",
                newName: "NoteShares");

            migrationBuilder.RenameTable(
                name: "Notes",
                schema: "notes",
                newName: "Notes");

            migrationBuilder.RenameTable(
                name: "NoteLinks",
                schema: "notes",
                newName: "NoteLinks");

            migrationBuilder.RenameTable(
                name: "NoteFolders",
                schema: "notes",
                newName: "NoteFolders");
        }
    }
}
