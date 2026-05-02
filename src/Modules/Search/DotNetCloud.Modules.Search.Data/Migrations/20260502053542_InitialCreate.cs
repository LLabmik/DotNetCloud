using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DotNetCloud.Modules.Search.Data.Migrations
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
                name: "IndexingJobs",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DocumentsProcessed = table.Column<int>(type: "integer", nullable: false),
                    DocumentsTotal = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexingJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SearchIndexEntries",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ModuleId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Content = table.Column<string>(type: "character varying(102400)", maxLength: 102400, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IndexedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MetadataJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchIndexEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_indexing_jobs_module_id",
                schema: "core",
                table: "IndexingJobs",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "ix_indexing_jobs_status",
                schema: "core",
                table: "IndexingJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_search_index_entity_type",
                schema: "core",
                table: "SearchIndexEntries",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "ix_search_index_module_entity",
                schema: "core",
                table: "SearchIndexEntries",
                columns: new[] { "ModuleId", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_search_index_module_id",
                schema: "core",
                table: "SearchIndexEntries",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "ix_search_index_organization_id",
                schema: "core",
                table: "SearchIndexEntries",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "ix_search_index_owner_id",
                schema: "core",
                table: "SearchIndexEntries",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_search_index_updated_at",
                schema: "core",
                table: "SearchIndexEntries",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndexingJobs",
                schema: "core");

            migrationBuilder.DropTable(
                name: "SearchIndexEntries",
                schema: "core");
        }
    }
}
