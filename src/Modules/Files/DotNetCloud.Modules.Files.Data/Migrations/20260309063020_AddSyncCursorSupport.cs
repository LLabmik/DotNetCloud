using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncCursorSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SyncSequence",
                table: "FileNodes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserSyncCounters",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSyncCounters", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_owner_sync_sequence",
                table: "FileNodes",
                columns: new[] { "OwnerId", "SyncSequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSyncCounters");

            migrationBuilder.DropIndex(
                name: "ix_file_nodes_owner_sync_sequence",
                table: "FileNodes");

            migrationBuilder.DropColumn(
                name: "SyncSequence",
                table: "FileNodes");
        }
    }
}
