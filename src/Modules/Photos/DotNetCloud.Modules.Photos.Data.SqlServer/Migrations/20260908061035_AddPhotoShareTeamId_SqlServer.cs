using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Photos.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoShareTeamId_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SharedWithTeamId",
                schema: "photos",
                table: "PhotoShares",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_photo_shares_shared_with_team",
                schema: "photos",
                table: "PhotoShares",
                column: "SharedWithTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_photo_shares_shared_with_team",
                schema: "photos",
                table: "PhotoShares");

            migrationBuilder.DropColumn(
                name: "SharedWithTeamId",
                schema: "photos",
                table: "PhotoShares");
        }
    }
}
