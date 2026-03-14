using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncHardeningP0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_file_nodes_parent_name",
                table: "FileNodes");

            migrationBuilder.CreateIndex(
                name: "uq_file_nodes_parent_name_active",
                table: "FileNodes",
                columns: new[] { "ParentId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ParentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_file_nodes_root_name_active",
                table: "FileNodes",
                columns: new[] { "OwnerId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ParentId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_file_chunks_ref_count_non_negative",
                table: "FileChunks",
                sql: "\"ReferenceCount\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_file_nodes_parent_name_active",
                table: "FileNodes");

            migrationBuilder.DropIndex(
                name: "uq_file_nodes_root_name_active",
                table: "FileNodes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_file_chunks_ref_count_non_negative",
                table: "FileChunks");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_parent_name",
                table: "FileNodes",
                columns: new[] { "ParentId", "Name" });
        }
    }
}
