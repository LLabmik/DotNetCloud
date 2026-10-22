using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultUserContent_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ExemptBytes",
                schema: "core",
                table: "FileQuotas",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "IsReadOnly",
                schema: "core",
                table: "FileNodes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserContentProvisioning",
                schema: "core",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TemplateFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ItemsCreated = table.Column<int>(type: "int", nullable: false),
                    ItemsSkipped = table.Column<int>(type: "int", nullable: false),
                    PendingUpdates = table.Column<int>(type: "int", nullable: false),
                    PendingDeletions = table.Column<int>(type: "int", nullable: false),
                    BytesCopied = table.Column<long>(type: "bigint", nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserContentProvisioning", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "UserDefaultContentItems",
                schema: "core",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelativePathKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProvisionedContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProvisionedSize = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstProvisionedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastCheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDefaultContentItems", x => new { x.UserId, x.RelativePathKey });
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_owner_is_read_only",
                schema: "core",
                table: "FileNodes",
                columns: new[] { "OwnerId", "IsReadOnly" });

            migrationBuilder.CreateIndex(
                name: "ix_user_content_provisioning_status_lease",
                schema: "core",
                table: "UserContentProvisioning",
                columns: new[] { "Status", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_user_default_content_items_file_node_id",
                schema: "core",
                table: "UserDefaultContentItems",
                column: "FileNodeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserContentProvisioning",
                schema: "core");

            migrationBuilder.DropTable(
                name: "UserDefaultContentItems",
                schema: "core");

            migrationBuilder.DropIndex(
                name: "ix_file_nodes_owner_is_read_only",
                schema: "core",
                table: "FileNodes");

            migrationBuilder.DropColumn(
                name: "ExemptBytes",
                schema: "core",
                table: "FileQuotas");

            migrationBuilder.DropColumn(
                name: "IsReadOnly",
                schema: "core",
                table: "FileNodes");
        }
    }
}
