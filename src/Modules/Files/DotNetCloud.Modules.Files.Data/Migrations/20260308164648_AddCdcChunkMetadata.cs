using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCdcChunkMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChunkSizesManifest",
                table: "UploadSessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChunkSize",
                table: "FileVersionChunks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "Offset",
                table: "FileVersionChunks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChunkSizesManifest",
                table: "UploadSessions");

            migrationBuilder.DropColumn(
                name: "ChunkSize",
                table: "FileVersionChunks");

            migrationBuilder.DropColumn(
                name: "Offset",
                table: "FileVersionChunks");
        }
    }
}
