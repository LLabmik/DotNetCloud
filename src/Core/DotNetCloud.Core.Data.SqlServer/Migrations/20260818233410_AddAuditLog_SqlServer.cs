using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Core.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddAuditLog_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    timestamp_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    caller_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    caller_user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    caller_roles = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    module_id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    action = table.Column<int>(type: "int", nullable: false),
                    entity_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_caller_user",
                table: "AuditLogs",
                column: "caller_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_entity",
                table: "AuditLogs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_module_timestamp",
                table: "AuditLogs",
                columns: new[] { "module_id", "timestamp_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_timestamp_utc",
                table: "AuditLogs",
                column: "timestamp_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
