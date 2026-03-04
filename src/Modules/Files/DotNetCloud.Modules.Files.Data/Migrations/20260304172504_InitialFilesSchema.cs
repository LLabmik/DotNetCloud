using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Files.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialFilesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileChunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Size = table.Column<int>(type: "integer", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReferenceCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastReferencedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileChunks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterializedPath = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Depth = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileNodes_FileNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "FileNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FileQuotas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxBytes = table.Column<long>(type: "bigint", nullable: false),
                    UsedBytes = table.Column<long>(type: "bigint", nullable: false),
                    LastCalculatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileQuotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UploadSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetFileNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TotalSize = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TotalChunks = table.Column<int>(type: "integer", nullable: false),
                    ReceivedChunks = table.Column<int>(type: "integer", nullable: false),
                    ChunkManifest = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileComments_FileComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "FileComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileComments_FileNodes_FileNodeId",
                        column: x => x.FileNodeId,
                        principalTable: "FileNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShareType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SharedWithUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SharedWithTeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    SharedWithGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Permission = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LinkToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LinkPasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    MaxDownloads = table.Column<int>(type: "integer", nullable: true),
                    DownloadCount = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileShares_FileNodes_FileNodeId",
                        column: x => x.FileNodeId,
                        principalTable: "FileNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileTags_FileNodes_FileNodeId",
                        column: x => x.FileNodeId,
                        principalTable: "FileNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileVersions_FileNodes_FileNodeId",
                        column: x => x.FileNodeId,
                        principalTable: "FileNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileVersionChunks",
                columns: table => new
                {
                    FileVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileVersionChunks", x => new { x.FileVersionId, x.FileChunkId, x.SequenceIndex });
                    table.ForeignKey(
                        name: "FK_FileVersionChunks_FileChunks_FileChunkId",
                        column: x => x.FileChunkId,
                        principalTable: "FileChunks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FileVersionChunks_FileVersions_FileVersionId",
                        column: x => x.FileVersionId,
                        principalTable: "FileVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_file_chunks_hash",
                table: "FileChunks",
                column: "ChunkHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_chunks_ref_count",
                table: "FileChunks",
                column: "ReferenceCount");

            migrationBuilder.CreateIndex(
                name: "ix_file_comments_created_at",
                table: "FileComments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_file_comments_created_by",
                table: "FileComments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_file_comments_file_node_id",
                table: "FileComments",
                column: "FileNodeId");

            migrationBuilder.CreateIndex(
                name: "ix_file_comments_parent_id",
                table: "FileComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_content_hash",
                table: "FileNodes",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_created_at",
                table: "FileNodes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_is_deleted",
                table: "FileNodes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_materialized_path",
                table: "FileNodes",
                column: "MaterializedPath");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_owner_favorite",
                table: "FileNodes",
                columns: new[] { "OwnerId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_owner_id",
                table: "FileNodes",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_parent_id",
                table: "FileNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_parent_name",
                table: "FileNodes",
                columns: new[] { "ParentId", "Name" });

            migrationBuilder.CreateIndex(
                name: "ix_file_nodes_updated_at",
                table: "FileNodes",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_file_quotas_user_id",
                table: "FileQuotas",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_created_by",
                table: "FileShares",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_expires_at",
                table: "FileShares",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_file_node_id",
                table: "FileShares",
                column: "FileNodeId");

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_link_token",
                table: "FileShares",
                column: "LinkToken",
                unique: true,
                filter: "[LinkToken] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_shared_with_team",
                table: "FileShares",
                column: "SharedWithTeamId");

            migrationBuilder.CreateIndex(
                name: "ix_file_shares_shared_with_user",
                table: "FileShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "ix_file_tags_created_by",
                table: "FileTags",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_file_tags_name",
                table: "FileTags",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "ix_file_tags_node_name_user",
                table: "FileTags",
                columns: new[] { "FileNodeId", "Name", "CreatedByUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_file_version_chunks_version_seq",
                table: "FileVersionChunks",
                columns: new[] { "FileVersionId", "SequenceIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_FileVersionChunks_FileChunkId",
                table: "FileVersionChunks",
                column: "FileChunkId");

            migrationBuilder.CreateIndex(
                name: "ix_file_versions_content_hash",
                table: "FileVersions",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "ix_file_versions_created_by",
                table: "FileVersions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_file_versions_file_node_id",
                table: "FileVersions",
                column: "FileNodeId");

            migrationBuilder.CreateIndex(
                name: "ix_file_versions_node_version",
                table: "FileVersions",
                columns: new[] { "FileNodeId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_expires_at",
                table: "UploadSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_status",
                table: "UploadSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_user_id",
                table: "UploadSessions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileComments");

            migrationBuilder.DropTable(
                name: "FileQuotas");

            migrationBuilder.DropTable(
                name: "FileShares");

            migrationBuilder.DropTable(
                name: "FileTags");

            migrationBuilder.DropTable(
                name: "FileVersionChunks");

            migrationBuilder.DropTable(
                name: "UploadSessions");

            migrationBuilder.DropTable(
                name: "FileChunks");

            migrationBuilder.DropTable(
                name: "FileVersions");

            migrationBuilder.DropTable(
                name: "FileNodes");
        }
    }
}
