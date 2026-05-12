using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Music.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                schema: "music",
                table: "Tracks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_tracks_content_hash",
                schema: "music",
                table: "Tracks",
                column: "ContentHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tracks_content_hash",
                schema: "music",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                schema: "music",
                table: "Tracks");
        }
    }
}
