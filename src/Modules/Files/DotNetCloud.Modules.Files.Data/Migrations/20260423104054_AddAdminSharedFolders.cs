using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminSharedFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminSharedFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourcePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CrawlMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastIndexedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextScheduledScanAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastScanStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReindexState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSharedFolders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminSharedFolderGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminSharedFolderId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSharedFolderGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminSharedFolderGrants_AdminSharedFolders_AdminSharedFolde~",
                        column: x => x.AdminSharedFolderId,
                        principalTable: "AdminSharedFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folder_grants_folder_group",
                table: "AdminSharedFolderGrants",
                columns: new[] { "AdminSharedFolderId", "GroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folder_grants_group_id",
                table: "AdminSharedFolderGrants",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folders_next_scan",
                table: "AdminSharedFolders",
                column: "NextScheduledScanAt");

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folders_org_display_name",
                table: "AdminSharedFolders",
                columns: new[] { "OrganizationId", "DisplayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folders_source_path",
                table: "AdminSharedFolders",
                column: "SourcePath",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminSharedFolderGrants");

            migrationBuilder.DropTable(
                name: "AdminSharedFolders");
        }
    }
}
