using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncFolderRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminSharedFolderCleanupStatuses",
                schema: "core",
                columns: table => new
                {
                    CleanupJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedFolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phase = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SearchDocsRemoved = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    SearchDocsTotal = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AffectedUsers = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UsersCleaned = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MediaEntitiesRemoved = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSharedFolderCleanupStatuses", x => x.CleanupJobId);
                });

            migrationBuilder.CreateTable(
                name: "SyncFolderRegistrations",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemoteFolderNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RemoteFolderPath = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncFolderRegistrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folder_cleanup_statuses_shared_folder_id",
                schema: "core",
                table: "AdminSharedFolderCleanupStatuses",
                column: "SharedFolderId");

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folder_cleanup_statuses_started_at",
                schema: "core",
                table: "AdminSharedFolderCleanupStatuses",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "ix_sync_folder_registrations_user_id",
                schema: "core",
                table: "SyncFolderRegistrations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "uq_sync_folder_registrations_user_folder",
                schema: "core",
                table: "SyncFolderRegistrations",
                columns: new[] { "UserId", "RemoteFolderNodeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminSharedFolderCleanupStatuses",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SyncFolderRegistrations",
                schema: "core");
        }
    }
}
