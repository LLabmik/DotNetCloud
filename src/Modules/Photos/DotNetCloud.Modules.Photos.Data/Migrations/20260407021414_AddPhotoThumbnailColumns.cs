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
                schema: "photos",
                table: "Photos",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "ThumbnailGrid",
                schema: "photos",
                table: "Photos",
                type: "bytea",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThumbnailDetail",
                schema: "photos",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "ThumbnailGrid",
                schema: "photos",
                table: "Photos");
        }
    }
}
