using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Photos.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixFileNodeOwnerUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_photos_file_node_id",
                schema: "photos",
                table: "Photos");

            migrationBuilder.CreateIndex(
                name: "uq_photos_file_node_owner_id",
                schema: "photos",
                table: "Photos",
                columns: new[] { "FileNodeId", "OwnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_photos_file_node_owner_id",
                schema: "photos",
                table: "Photos");

            migrationBuilder.CreateIndex(
                name: "uq_photos_file_node_id",
                schema: "photos",
                table: "Photos",
                column: "FileNodeId",
                unique: true);
        }
    }
}
