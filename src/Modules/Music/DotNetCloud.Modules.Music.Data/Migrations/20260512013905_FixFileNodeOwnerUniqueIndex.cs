using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Music.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixFileNodeOwnerUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_tracks_file_node_id",
                schema: "music",
                table: "Tracks");

            migrationBuilder.CreateIndex(
                name: "uq_tracks_file_node_owner_id",
                schema: "music",
                table: "Tracks",
                columns: new[] { "FileNodeId", "OwnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_tracks_file_node_owner_id",
                schema: "music",
                table: "Tracks");

            migrationBuilder.CreateIndex(
                name: "uq_tracks_file_node_id",
                schema: "music",
                table: "Tracks",
                column: "FileNodeId",
                unique: true);
        }
    }
}
