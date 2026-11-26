using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Music.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyMusicTables : Migration
    {
        /// <summary>
        /// Drops the pre-canonical-library music tables. They were replaced by
        /// <c>user_tracks</c>/<c>canonical_*</c>, are no longer part of the model, and are empty on
        /// every installation that has been running since the switch.
        /// </summary>
        /// <param name="migrationBuilder">The migration builder.</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarded so a fresh database (where these tables never existed) is a no-op.
            migrationBuilder.Sql("DROP TABLE IF EXISTS music.track_artists CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS music.track_genres CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS music.tracks CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS music.albums CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS music.artists CASCADE;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS music.genres CASCADE;");
        }

        /// <summary>
        /// Intentionally a no-op: the dropped tables were dead schema, so there is nothing to restore.
        /// Rolling this migration back leaves them absent.
        /// </summary>
        /// <param name="migrationBuilder">The migration builder.</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
