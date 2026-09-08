using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Notes.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteShareTeamId_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_note_shares_note_user",
                schema: "core",
                table: "NoteShares");

            migrationBuilder.AddColumn<Guid>(
                name: "SharedWithTeamId",
                schema: "core",
                table: "NoteShares",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_note_shares_note_team",
                schema: "core",
                table: "NoteShares",
                columns: new[] { "NoteId", "SharedWithTeamId" });

            migrationBuilder.CreateIndex(
                name: "ix_note_shares_note_user",
                schema: "core",
                table: "NoteShares",
                columns: new[] { "NoteId", "SharedWithUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_note_shares_note_team",
                schema: "core",
                table: "NoteShares");

            migrationBuilder.DropIndex(
                name: "ix_note_shares_note_user",
                schema: "core",
                table: "NoteShares");

            migrationBuilder.DropColumn(
                name: "SharedWithTeamId",
                schema: "core",
                table: "NoteShares");

            migrationBuilder.CreateIndex(
                name: "ix_note_shares_note_user",
                schema: "core",
                table: "NoteShares",
                columns: new[] { "NoteId", "SharedWithUserId" },
                unique: true);
        }
    }
}
