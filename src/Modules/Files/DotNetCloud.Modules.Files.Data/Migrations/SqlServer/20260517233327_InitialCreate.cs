using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "AdminSharedFolders",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourcePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CrawlMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    LastIndexedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextScheduledScanAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastScanStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReindexState = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSharedFolders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileChunks",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChunkHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReferenceCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastReferencedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileChunks", x => x.Id);
                    table.CheckConstraint("ck_file_chunks_ref_count_non_negative", "\"ReferenceCount\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "FileQuotas",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaxBytes = table.Column<long>(type: "bigint", nullable: false),
                    UsedBytes = table.Column<long>(type: "bigint", nullable: false),
                    LastCalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileQuotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncDevices",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClientVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UploadSessions",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetFileNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TotalSize = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TotalChunks = table.Column<int>(type: "int", nullable: false),
                    ReceivedChunks = table.Column<int>(type: "int", nullable: false),
                    ChunkManifest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChunkSizesManifest = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PosixMode = table.Column<int>(type: "int", nullable: true),
                    PosixOwnerHint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSyncCounters",
                schema: "core",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSyncCounters", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "AdminSharedFolderGrants",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdminSharedFolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminSharedFolderGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminSharedFolderGrants_AdminSharedFolders_AdminSharedFolderId",
                        column: x => x.AdminSharedFolderId,
                        principalSchema: "core",
                        principalTable: "AdminSharedFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MountedNodeEntries",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedFolderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    IsDirectory = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MountedNodeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MountedNodeEntries_AdminSharedFolders_SharedFolderId",
                        column: x => x.SharedFolderId,
                        principalSchema: "core",
                        principalTable: "AdminSharedFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileNodes",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NodeType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaterializedPath = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Depth = table.Column<int>(type: "int", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CurrentVersion = table.Column<int>(type: "int", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsFavorite = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    SyncSequence = table.Column<long>(type: "bigint", nullable: true),
                    OriginatingDeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LinkTarget = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PosixMode = table.Column<int>(type: "int", nullable: true),
                    PosixOwnerHint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileNodes_FileNodes_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "core",
                        principalTable: "FileNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileNodes_SyncDevices_OriginatingDeviceId",
                        column: x => x.OriginatingDeviceId,
                        principalSchema: "core",
                        principalTable: "SyncDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SyncDeviceCursors",
                schema: "core",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastAcknowledgedSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncDeviceCursors", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_SyncDeviceCursors_SyncDevices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "core",
                        principalTable: "SyncDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileComments",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileComments_FileComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalSchema: "core",
                        principalTable: "FileComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileComments_FileNodes_FileNodeId",
                        column: x => x.FileNodeId,
                        principalSchema: "core",
                        principalTable: "FileNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileShares",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShareType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SharedWithUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SharedWithTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SharedWithGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Permission = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LinkToken = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LinkPasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MaxDownloads = table.Column<int>(type: "int", nullable: true),
                    DownloadCount = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryNotificationSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileShares_FileNodes_FileNodeId",
                        column: x => x.FileNodeId,
                        principalSchema: "core",
                        principalTable: "FileNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileTags",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileTags_FileNodes_FileNodeId",
                        column: x => x.FileNodeId,
                        principalSchema: "core",
                        principalTable: "FileNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileVersions",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ScanStatus = table.Column<int>(type: "int", nullable: true),
                    PosixMode = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileVersions_FileNodes_FileNodeId",
                        column: x => x.FileNodeId,
                        principalSchema: "core",
                        principalTable: "FileNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileVersionChunks",
                schema: "core",
                columns: table => new
                {
                    FileVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileChunkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceIndex = table.Column<int>(type: "int", nullable: false),
                    Offset = table.Column<long>(type: "bigint", nullable: false),
                    ChunkSize = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileVersionChunks", x => new { x.FileVersionId, x.FileChunkId, x.SequenceIndex });
                    table.ForeignKey(
                        name: "FK_FileVersionChunks_FileChunks_FileChunkId",
                        column: x => x.FileChunkId,
                        principalSchema: "core",
                        principalTable: "FileChunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileVersionChunks_FileVersions_FileVersionId",
                        column: x => x.FileVersionId,
                        principalSchema: "core",
                        principalTable: "FileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folder_grants_folder_group",
                schema: "core",
                table: "AdminSharedFolderGrants",
                columns: new[] { "AdminSharedFolderId", "GroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folder_grants_group_id",
                schema: "core",
                table: "AdminSharedFolderGrants",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folders_next_scan",
                schema: "core",
                table: "AdminSharedFolders",
                column: "NextScheduledScanAt");

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folders_org_display_name",
                schema: "core",
                table: "AdminSharedFolders",
                columns: new[] { "OrganizationId", "DisplayName" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_admin_shared_folders_source_path",
                schema: "core",
                table: "AdminSharedFolders",
                column: "SourcePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_chunks_hash",
                schema: "core",
                table: "FileChunks",
                column: "ChunkHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_chunks_ref_count",
                schema: "core",
                table: "FileChunks",
                column: "ReferenceCount");

            migrationBuilder.CreateIndex(
                name: "ix_file_comments_created_at",
                schema: "core",
                table: "FileComments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_file_comments_created_by",
                schema: "core",
                table: "FileComments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_file_comments_file_node_id",
                schema: "core",
                table: "FileComments",
                column: "FileNodeId");

            migrationBuilder.CreateIndex(
                name: "ix_file_comments_parent_id",
                schema: "core",
                table: "FileComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_content_hash",
                schema: "core",
                table: "FileNodes",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_created_at",
                schema: "core",
                table: "FileNodes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_is_deleted",
                schema: "core",
                table: "FileNodes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_materialized_path",
                schema: "core",
                table: "FileNodes",
                column: "MaterializedPath");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_originating_device_id",
                schema: "core",
                table: "FileNodes",
                column: "OriginatingDeviceId",
                filter: "\"OriginatingDeviceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_owner_favorite",
                schema: "core",
                table: "FileNodes",
                columns: new[] { "OwnerId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_owner_id",
                schema: "core",
                table: "FileNodes",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_owner_sync_sequence",
                schema: "core",
                table: "FileNodes",
                columns: new[] { "OwnerId", "SyncSequence" });

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_parent_id",
                schema: "core",
                table: "FileNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_updated_at",
                schema: "core",
                table: "FileNodes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "uq_file_nodes_parent_name_active",
                schema: "core",
                table: "FileNodes",
                columns: new[] { "ParentId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ParentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_file_nodes_root_name_active",
                schema: "core",
                table: "FileNodes",
                columns: new[] { "OwnerId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ParentId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_file_quotas_user_id",
                schema: "core",
                table: "FileQuotas",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_created_by",
                schema: "core",
                table: "FileShares",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_expires_at",
                schema: "core",
                table: "FileShares",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_file_node_id",
                schema: "core",
                table: "FileShares",
                column: "FileNodeId");

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_link_token",
                schema: "core",
                table: "FileShares",
                column: "LinkToken",
                unique: true,
                filter: "\"LinkToken\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_shared_with_team",
                schema: "core",
                table: "FileShares",
                column: "SharedWithTeamId");

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_shared_with_user",
                schema: "core",
                table: "FileShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "ix_file_tags_created_by",
                schema: "core",
                table: "FileTags",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_file_tags_name",
                schema: "core",
                table: "FileTags",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "ix_file_tags_node_name_user",
                schema: "core",
                table: "FileTags",
                columns: new[] { "FileNodeId", "Name", "CreatedByUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_version_chunks_version_seq",
                schema: "core",
                table: "FileVersionChunks",
                columns: new[] { "FileVersionId", "SequenceIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_FileVersionChunks_FileChunkId",
                schema: "core",
                table: "FileVersionChunks",
                column: "FileChunkId");

            migrationBuilder.CreateIndex(
                name: "ix_file_versions_content_hash",
                schema: "core",
                table: "FileVersions",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_file_versions_created_by",
                schema: "core",
                table: "FileVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_file_versions_file_node_id",
                schema: "core",
                table: "FileVersions",
                column: "FileNodeId");

            migrationBuilder.CreateIndex(
                name: "ix_file_versions_node_version",
                schema: "core",
                table: "FileVersions",
                columns: new[] { "FileNodeId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mounted_node_entries_shared_folder_id",
                schema: "core",
                table: "MountedNodeEntries",
                column: "SharedFolderId");

            migrationBuilder.CreateIndex(
                name: "ix_sync_device_cursors_user_id",
                schema: "core",
                table: "SyncDeviceCursors",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_sync_devices_user_active",
                schema: "core",
                table: "SyncDevices",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "ix_sync_devices_user_id",
                schema: "core",
                table: "SyncDevices",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_device_id",
                schema: "core",
                table: "UploadSessions",
                column: "DeviceId",
                filter: "\"DeviceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_expires_at",
                schema: "core",
                table: "UploadSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_status",
                schema: "core",
                table: "UploadSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_user_id",
                schema: "core",
                table: "UploadSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminSharedFolderGrants",
                schema: "core");

            migrationBuilder.DropTable(
                name: "FileComments",
                schema: "core");

            migrationBuilder.DropTable(
                name: "FileQuotas",
                schema: "core");

            migrationBuilder.DropTable(
                name: "FileShares",
                schema: "core");

            migrationBuilder.DropTable(
                name: "FileTags",
                schema: "core");

            migrationBuilder.DropTable(
                name: "FileVersionChunks",
                schema: "core");

            migrationBuilder.DropTable(
                name: "MountedNodeEntries",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SyncDeviceCursors",
                schema: "core");

            migrationBuilder.DropTable(
                name: "UploadSessions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "UserSyncCounters",
                schema: "core");

            migrationBuilder.DropTable(
                name: "FileChunks",
                schema: "core");

            migrationBuilder.DropTable(
                name: "FileVersions",
                schema: "core");

            migrationBuilder.DropTable(
                name: "AdminSharedFolders",
                schema: "core");

            migrationBuilder.DropTable(
                name: "FileNodes",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SyncDevices",
                schema: "core");
        }
    }
}
