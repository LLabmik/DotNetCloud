using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleDataStoreOrphanSupport_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_module_data_stores_module_id",
                table: "ModuleDataStores");

            migrationBuilder.CreateIndex(
                name: "ix_module_data_stores_module_id",
                table: "ModuleDataStores",
                column: "ModuleId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_module_data_stores_module_id",
                table: "ModuleDataStores");

            migrationBuilder.AddForeignKey(
                name: "fk_module_data_stores_module_id",
                table: "ModuleDataStores",
                column: "ModuleId",
                principalTable: "InstalledModules",
                principalColumn: "ModuleId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
