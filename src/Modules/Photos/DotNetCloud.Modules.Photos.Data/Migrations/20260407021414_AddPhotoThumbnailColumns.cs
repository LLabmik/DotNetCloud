using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Photos.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoThumbnailColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ThumbnailDetail",
                table: "Photos",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ThumbnailGrid",
                table: "Photos",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThumbnailDetail",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "ThumbnailGrid",
                table: "Photos");
        }
    }
}
