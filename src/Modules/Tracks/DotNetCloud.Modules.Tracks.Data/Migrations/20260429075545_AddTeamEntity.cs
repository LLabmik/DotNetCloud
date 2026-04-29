using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Tracks.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TracksTeams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TracksTeams", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tracks_teams_name",
                table: "TracksTeams",
                column: "Name");

            // Backfill TracksTeams rows from existing TeamRoles so the FK can be created.
            // Without this, any TeamRoles row with a TeamId not in TracksTeams would
            // cause a FK violation (23503) when the constraint is added.
            migrationBuilder.Sql(
                """
                INSERT INTO "TracksTeams" ("Id", "Name", "CreatedAt", "CreatedByUserId")
                SELECT DISTINCT
                    tr."TeamId",
                    'Team ' || left(tr."TeamId"::text, 8),
                    NOW(),
                    '00000000-0000-0000-0000-000000000000'::uuid
                FROM "TeamRoles" tr
                WHERE tr."TeamId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "TracksTeams" t WHERE t."Id" = tr."TeamId")
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamRoles_TracksTeams_TeamId",
                table: "TeamRoles",
                column: "TeamId",
                principalTable: "TracksTeams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeamRoles_TracksTeams_TeamId",
                table: "TeamRoles");

            migrationBuilder.DropTable(
                name: "TracksTeams");
        }
    }
}
