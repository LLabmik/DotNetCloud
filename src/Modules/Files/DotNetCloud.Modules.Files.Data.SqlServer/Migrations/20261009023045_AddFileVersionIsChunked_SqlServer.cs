using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFileVersionIsChunked_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsChunked",
                schema: "core",
                table: "FileVersions",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_versions_is_chunked",
                schema: "core",
                table: "FileVersions",
                column: "IsChunked");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_file_versions_is_chunked",
                schema: "core",
                table: "FileVersions");

            migrationBuilder.DropColumn(
                name: "IsChunked",
                schema: "core",
                table: "FileVersions");
        }
    }
}
