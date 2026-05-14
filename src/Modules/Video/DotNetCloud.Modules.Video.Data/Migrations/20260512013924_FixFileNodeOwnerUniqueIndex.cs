using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Video.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixFileNodeOwnerUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_videos_file_node_id",
                schema: "video",
                table: "Videos");

            migrationBuilder.CreateIndex(
                name: "uq_videos_file_node_owner_id",
                schema: "video",
                table: "Videos",
                columns: new[] { "FileNodeId", "OwnerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_videos_file_node_owner_id",
                schema: "video",
                table: "Videos");

            migrationBuilder.CreateIndex(
                name: "uq_videos_file_node_id",
                schema: "video",
                table: "Videos",
                column: "FileNodeId",
                unique: true);
        }
    }
}
