using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAllUsersBuiltInGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAllUsersGroup",
                table: "Groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_groups_org_all_users",
                table: "Groups",
                columns: new[] { "OrganizationId", "IsAllUsersGroup" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_groups_org_all_users",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "IsAllUsersGroup",
                table: "Groups");
        }
    }
}
