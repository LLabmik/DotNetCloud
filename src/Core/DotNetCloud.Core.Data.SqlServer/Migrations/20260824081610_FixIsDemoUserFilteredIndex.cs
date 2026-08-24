using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Core.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class FixIsDemoUserFilteredIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_IsDemoUser",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_IsDemoUser",
                table: "AspNetUsers",
                column: "IsDemoUser",
                filter: "[IsDemoUser] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApplicationUsers_IsDemoUser",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUsers_IsDemoUser",
                table: "AspNetUsers",
                column: "IsDemoUser",
                filter: "[IsDemoUser] = 1");
        }
    }
}
