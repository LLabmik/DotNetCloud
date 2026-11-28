using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Photos.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoShareTeamId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SharedWithTeamId",
                table: "PhotoShares",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_photo_shares_shared_with_team",
                table: "PhotoShares",
                column: "SharedWithTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_photo_shares_shared_with_team",
                table: "PhotoShares");

            migrationBuilder.DropColumn(
                name: "SharedWithTeamId",
                table: "PhotoShares");
        }
    }
}
