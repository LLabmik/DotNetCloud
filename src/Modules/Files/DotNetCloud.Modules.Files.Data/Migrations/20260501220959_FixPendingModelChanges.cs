using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.RenameTable(
                name: "UserSyncCounters",
                newName: "UserSyncCounters",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "UploadSessions",
                newName: "UploadSessions",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "SyncDevices",
                newName: "SyncDevices",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "SyncDeviceCursors",
                newName: "SyncDeviceCursors",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "MountedNodeEntries",
                newName: "MountedNodeEntries",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "FileVersions",
                newName: "FileVersions",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "FileVersionChunks",
                newName: "FileVersionChunks",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "FileTags",
                newName: "FileTags",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "FileShares",
                newName: "FileShares",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "FileQuotas",
                newName: "FileQuotas",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "FileNodes",
                newName: "FileNodes",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "FileComments",
                newName: "FileComments",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "FileChunks",
                newName: "FileChunks",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "AdminSharedFolders",
                newName: "AdminSharedFolders",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "AdminSharedFolderGrants",
                newName: "AdminSharedFolderGrants",
                newSchema: "core");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "UserSyncCounters",
                schema: "core",
                newName: "UserSyncCounters");

            migrationBuilder.RenameTable(
                name: "UploadSessions",
                schema: "core",
                newName: "UploadSessions");

            migrationBuilder.RenameTable(
                name: "SyncDevices",
                schema: "core",
                newName: "SyncDevices");

            migrationBuilder.RenameTable(
                name: "SyncDeviceCursors",
                schema: "core",
                newName: "SyncDeviceCursors");

            migrationBuilder.RenameTable(
                name: "MountedNodeEntries",
                schema: "core",
                newName: "MountedNodeEntries");

            migrationBuilder.RenameTable(
                name: "FileVersions",
                schema: "core",
                newName: "FileVersions");

            migrationBuilder.RenameTable(
                name: "FileVersionChunks",
                schema: "core",
                newName: "FileVersionChunks");

            migrationBuilder.RenameTable(
                name: "FileTags",
                schema: "core",
                newName: "FileTags");

            migrationBuilder.RenameTable(
                name: "FileShares",
                schema: "core",
                newName: "FileShares");

            migrationBuilder.RenameTable(
                name: "FileQuotas",
                schema: "core",
                newName: "FileQuotas");

            migrationBuilder.RenameTable(
                name: "FileNodes",
                schema: "core",
                newName: "FileNodes");

            migrationBuilder.RenameTable(
                name: "FileComments",
                schema: "core",
                newName: "FileComments");

            migrationBuilder.RenameTable(
                name: "FileChunks",
                schema: "core",
                newName: "FileChunks");

            migrationBuilder.RenameTable(
                name: "AdminSharedFolders",
                schema: "core",
                newName: "AdminSharedFolders");

            migrationBuilder.RenameTable(
                name: "AdminSharedFolderGrants",
                schema: "core",
                newName: "AdminSharedFolderGrants");
        }
    }
}
