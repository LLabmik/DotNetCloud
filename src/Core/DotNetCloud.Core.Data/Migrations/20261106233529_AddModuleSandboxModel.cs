using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleSandboxModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DataStoreMode",
                table: "InstalledModules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "PublisherId",
                table: "InstalledModules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SandboxStatus",
                table: "InstalledModules",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<string>(
                name: "SignedByKeyId",
                table: "InstalledModules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrustTier",
                table: "InstalledModules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Bundled");

            migrationBuilder.CreateTable(
                name: "ModuleDataStores",
                columns: table => new
                {
                    ModuleId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SchemaName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PrincipalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EncryptedConnectionString = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleDataStores", x => x.ModuleId);
                    table.ForeignKey(
                        name: "fk_module_data_stores_module_id",
                        column: x => x.ModuleId,
                        principalTable: "InstalledModules",
                        principalColumn: "ModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModuleQuotas",
                columns: table => new
                {
                    ModuleId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MemoryLimitMb = table.Column<int>(type: "integer", nullable: true),
                    CpuPercent = table.Column<int>(type: "integer", nullable: true),
                    DataDirMb = table.Column<int>(type: "integer", nullable: true),
                    DatabaseMb = table.Column<int>(type: "integer", nullable: true),
                    ConnectionLimit = table.Column<int>(type: "integer", nullable: true),
                    RequestsPerMinute = table.Column<int>(type: "integer", nullable: true),
                    EventsPerMinute = table.Column<int>(type: "integer", nullable: true),
                    NotificationsPerMinute = table.Column<int>(type: "integer", nullable: true),
                    AuditEntriesPerMinute = table.Column<int>(type: "integer", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleQuotas", x => x.ModuleId);
                    table.ForeignKey(
                        name: "fk_module_quotas_module_id",
                        column: x => x.ModuleId,
                        principalTable: "InstalledModules",
                        principalColumn: "ModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModuleSettings",
                columns: table => new
                {
                    ModuleId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    IsSecret = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleSettings", x => new { x.ModuleId, x.Key });
                    table.ForeignKey(
                        name: "fk_module_settings_module_id",
                        column: x => x.ModuleId,
                        principalTable: "InstalledModules",
                        principalColumn: "ModuleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserErasureRequests",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserErasureRequests", x => x.RequestId);
                });

            migrationBuilder.CreateTable(
                name: "ModuleUserErasures",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleUserErasures", x => new { x.RequestId, x.ModuleId });
                    table.ForeignKey(
                        name: "fk_module_user_erasures_request_id",
                        column: x => x.RequestId,
                        principalTable: "UserErasureRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_installed_modules_sandbox_status",
                table: "InstalledModules",
                column: "SandboxStatus");

            migrationBuilder.CreateIndex(
                name: "ix_installed_modules_trust_tier",
                table: "InstalledModules",
                column: "TrustTier");

            migrationBuilder.CreateIndex(
                name: "ix_module_data_stores_status",
                table: "ModuleDataStores",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_module_settings_module_id",
                table: "ModuleSettings",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "ix_module_user_erasures_status",
                table: "ModuleUserErasures",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_user_erasure_requests_status",
                table: "UserErasureRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_user_erasure_requests_user_id",
                table: "UserErasureRequests",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModuleDataStores");

            migrationBuilder.DropTable(
                name: "ModuleQuotas");

            migrationBuilder.DropTable(
                name: "ModuleSettings");

            migrationBuilder.DropTable(
                name: "ModuleUserErasures");

            migrationBuilder.DropTable(
                name: "UserErasureRequests");

            migrationBuilder.DropIndex(
                name: "ix_installed_modules_sandbox_status",
                table: "InstalledModules");

            migrationBuilder.DropIndex(
                name: "ix_installed_modules_trust_tier",
                table: "InstalledModules");

            migrationBuilder.DropColumn(
                name: "DataStoreMode",
                table: "InstalledModules");

            migrationBuilder.DropColumn(
                name: "PublisherId",
                table: "InstalledModules");

            migrationBuilder.DropColumn(
                name: "SandboxStatus",
                table: "InstalledModules");

            migrationBuilder.DropColumn(
                name: "SignedByKeyId",
                table: "InstalledModules");

            migrationBuilder.DropColumn(
                name: "TrustTier",
                table: "InstalledModules");
        }
    }
}
