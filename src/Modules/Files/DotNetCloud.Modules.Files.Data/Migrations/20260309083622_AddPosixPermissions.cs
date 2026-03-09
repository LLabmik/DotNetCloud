using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPosixPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PosixMode",
                table: "UploadSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PosixOwnerHint",
                table: "UploadSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PosixMode",
                table: "FileVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PosixMode",
                table: "FileNodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PosixOwnerHint",
                table: "FileNodes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PosixMode",
                table: "UploadSessions");

            migrationBuilder.DropColumn(
                name: "PosixOwnerHint",
                table: "UploadSessions");

            migrationBuilder.DropColumn(
                name: "PosixMode",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "PosixMode",
                table: "FileNodes");

            migrationBuilder.DropColumn(
                name: "PosixOwnerHint",
                table: "FileNodes");
        }
    }
}
