using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Video.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCanonicalVideoTmdbId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TmdbId",
                schema: "video",
                table: "canonical_videos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_canonical_videos_tmdb_id",
                schema: "video",
                table: "canonical_videos",
                column: "TmdbId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_canonical_videos_tmdb_id",
                schema: "video",
                table: "canonical_videos");

            migrationBuilder.DropColumn(
                name: "TmdbId",
                schema: "video",
                table: "canonical_videos");
        }
    }
}
