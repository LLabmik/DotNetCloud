using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Tracks.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty after the Track C data-plane flip. This migration only re-qualified
            // every table into the core-managed "tracks" schema; the module now owns a schema of its
            // own and the provisioned connection string decides where unqualified DDL lands, so the
            // renames have nothing left to do. The migration id is preserved because an adopted
            // install's __EFMigrationsHistory already contains it.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty after the Track C data-plane flip. This migration only re-qualified
            // every table into the core-managed "tracks" schema; the module now owns a schema of its
            // own and the provisioned connection string decides where unqualified DDL lands, so the
            // renames have nothing left to do. The migration id is preserved because an adopted
            // install's __EFMigrationsHistory already contains it.
        }
    }
}
