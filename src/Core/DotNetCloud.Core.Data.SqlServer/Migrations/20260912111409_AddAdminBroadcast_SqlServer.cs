using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminBroadcast_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminBroadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ScheduledForUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminBroadcasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminBroadcastDismissals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    BroadcastId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DismissedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminBroadcastDismissals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminBroadcastDismissals_AdminBroadcasts_BroadcastId",
                        column: x => x.BroadcastId,
                        principalTable: "AdminBroadcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_broadcast_dismissals_broadcast_user",
                table: "AdminBroadcastDismissals",
                columns: new[] { "BroadcastId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_admin_broadcast_dismissals_user",
                table: "AdminBroadcastDismissals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_admin_broadcasts_pending",
                table: "AdminBroadcasts",
                columns: new[] { "SentAtUtc", "ScheduledForUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_admin_broadcasts_sent_at",
                table: "AdminBroadcasts",
                column: "SentAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminBroadcastDismissals");

            migrationBuilder.DropTable(
                name: "AdminBroadcasts");
        }
    }
}
