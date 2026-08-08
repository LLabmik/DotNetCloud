using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Notes.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class PromoteNotesToRequiredModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.RenameTable(
                name: "NoteVersions",
                schema: "notes",
                newName: "NoteVersions",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "NoteTags",
                schema: "notes",
                newName: "NoteTags",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "NoteShares",
                schema: "notes",
                newName: "NoteShares",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "Notes",
                schema: "notes",
                newName: "Notes",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "NoteLinks",
                schema: "notes",
                newName: "NoteLinks",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "NoteFolders",
                schema: "notes",
                newName: "NoteFolders",
                newSchema: "core");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notes");

            migrationBuilder.RenameTable(
                name: "NoteVersions",
                schema: "core",
                newName: "NoteVersions",
                newSchema: "notes");

            migrationBuilder.RenameTable(
                name: "NoteTags",
                schema: "core",
                newName: "NoteTags",
                newSchema: "notes");

            migrationBuilder.RenameTable(
                name: "NoteShares",
                schema: "core",
                newName: "NoteShares",
                newSchema: "notes");

            migrationBuilder.RenameTable(
                name: "Notes",
                schema: "core",
                newName: "Notes",
                newSchema: "notes");

            migrationBuilder.RenameTable(
                name: "NoteLinks",
                schema: "core",
                newName: "NoteLinks",
                newSchema: "notes");

            migrationBuilder.RenameTable(
                name: "NoteFolders",
                schema: "core",
                newName: "NoteFolders",
                newSchema: "notes");
        }
    }
}
