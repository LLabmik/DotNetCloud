using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddMountedNodeEntriesUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_mounted_node_entries_unique_path",
                schema: "core",
                table: "MountedNodeEntries",
                columns: new[] { "SharedFolderId", "RelativePath", "IsDirectory" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_mounted_node_entries_unique_path",
                schema: "core",
                table: "MountedNodeEntries");
        }
    }
}
