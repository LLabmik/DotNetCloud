using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddFileNodeCapturedAt_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CapturedAtUtc",
                schema: "core",
                table: "FileNodes",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CapturedAtUtc",
                schema: "core",
                table: "FileNodes");
        }
    }
}
