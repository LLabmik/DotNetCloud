using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Music.Data.SqlServer.Migrations
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
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[music].[TrackArtists]', N'U') IS NOT NULL DROP TABLE [music].[TrackArtists];");
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[music].[TrackGenres]', N'U') IS NOT NULL DROP TABLE [music].[TrackGenres];");
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[music].[Tracks]', N'U') IS NOT NULL DROP TABLE [music].[Tracks];");
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[music].[Albums]', N'U') IS NOT NULL DROP TABLE [music].[Albums];");
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[music].[Artists]', N'U') IS NOT NULL DROP TABLE [music].[Artists];");
            migrationBuilder.Sql(@"IF OBJECT_ID(N'[music].[Genres]', N'U') IS NOT NULL DROP TABLE [music].[Genres];");
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
