using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncDeviceIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DeviceId",
                table: "UploadSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginatingDeviceId",
                table: "FileNodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SyncDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncDevices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_device_id",
                table: "UploadSessions",
                column: "DeviceId",
                filter: "\"DeviceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_originating_device_id",
                table: "FileNodes",
                column: "OriginatingDeviceId",
                filter: "\"OriginatingDeviceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sync_devices_user_active",
                table: "SyncDevices",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_sync_devices_user_id",
                table: "SyncDevices",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileNodes_SyncDevices_OriginatingDeviceId",
                table: "FileNodes",
                column: "OriginatingDeviceId",
                principalTable: "SyncDevices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileNodes_SyncDevices_OriginatingDeviceId",
                table: "FileNodes");

            migrationBuilder.DropTable(
                name: "SyncDevices");

            migrationBuilder.DropIndex(
                name: "ix_upload_sessions_device_id",
                table: "UploadSessions");

            migrationBuilder.DropIndex(
                name: "ix_file_nodes_originating_device_id",
                table: "FileNodes");

            migrationBuilder.DropColumn(
                name: "DeviceId",
                table: "UploadSessions");

            migrationBuilder.DropColumn(
                name: "OriginatingDeviceId",
                table: "FileNodes");
        }
    }
}
