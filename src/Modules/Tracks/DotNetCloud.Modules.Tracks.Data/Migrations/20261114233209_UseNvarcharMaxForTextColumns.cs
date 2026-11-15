using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Tracks.Data.Migrations
{
    /// <inheritdoc />
    public partial class UseNvarcharMaxForTextColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. PostgreSQL stores an unbounded string as `text`, which is its normal,
            // comparable type, so dropping the explicit HasColumnType("text") from the entity
            // configuration leaves the PostgreSQL schema untouched. The SQL Server twin of this
            // migration is the one that alters the columns from the deprecated, non-comparable `text`
            // to `nvarchar(max)`.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
