using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFileVersionScanStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_file_shares_link_token",
                table: "FileShares");

            migrationBuilder.AddColumn<int>(
                name: "ScanStatus",
                table: "FileVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_link_token",
                table: "FileShares",
                column: "LinkToken",
                unique: true,
                filter: "\"LinkToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_file_shares_link_token",
                table: "FileShares");

            migrationBuilder.DropColumn(
                name: "ScanStatus",
                table: "FileVersions");

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_link_token",
                table: "FileShares",
                column: "LinkToken",
                unique: true,
                filter: "[LinkToken] IS NOT NULL");
        }
    }
}
