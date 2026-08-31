using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: the SearchIndexEntries / IndexingJobs tables already exist in
            // production (created by the old Search module migrations). Each DDL
            // statement is guarded so applying this migration on a database that
            // already has the tables is a no-op.
            migrationBuilder.Sql(@"
IF SCHEMA_ID(N'core') IS NULL EXEC('CREATE SCHEMA [core]');

IF OBJECT_ID(N'core.IndexingJobs', N'U') IS NULL
BEGIN
    CREATE TABLE [core].[IndexingJobs] (
        [Id] uniqueidentifier NOT NULL DEFAULT NEWSEQUENTIALID(),
        [ModuleId] nvarchar(50) NULL,
        [Type] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [StartedAt] datetimeoffset NULL,
        [CompletedAt] datetimeoffset NULL,
        [DocumentsProcessed] int NOT NULL,
        [DocumentsTotal] int NOT NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        CONSTRAINT [PK_IndexingJobs] PRIMARY KEY ([Id])
    );

    CREATE INDEX [ix_indexing_jobs_status] ON [core].[IndexingJobs] ([Status]);
    CREATE INDEX [ix_indexing_jobs_module_id] ON [core].[IndexingJobs] ([ModuleId]);
END
");

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'core.SearchIndexEntries', N'U') IS NULL
BEGIN
    CREATE TABLE [core].[SearchIndexEntries] (
        [Id] bigint NOT NULL IDENTITY,
        [ModuleId] nvarchar(50) NOT NULL,
        [EntityId] nvarchar(64) NOT NULL,
        [EntityType] nvarchar(100) NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Summary] nvarchar(1000) NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [OrganizationId] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [IndexedAt] datetimeoffset NOT NULL,
        [MetadataJson] nvarchar(4000) NULL,
        CONSTRAINT [PK_SearchIndexEntries] PRIMARY KEY ([Id])
    );

    CREATE UNIQUE INDEX [ix_search_index_module_entity] ON [core].[SearchIndexEntries] ([ModuleId], [EntityId]);
    CREATE INDEX [ix_search_index_owner_id] ON [core].[SearchIndexEntries] ([OwnerId]);
    CREATE INDEX [ix_search_index_organization_id] ON [core].[SearchIndexEntries] ([OrganizationId]);
    CREATE INDEX [ix_search_index_module_id] ON [core].[SearchIndexEntries] ([ModuleId]);
    CREATE INDEX [ix_search_index_entity_type] ON [core].[SearchIndexEntries] ([EntityType]);
    CREATE INDEX [ix_search_index_updated_at] ON [core].[SearchIndexEntries] ([UpdatedAt]);
END
");
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
