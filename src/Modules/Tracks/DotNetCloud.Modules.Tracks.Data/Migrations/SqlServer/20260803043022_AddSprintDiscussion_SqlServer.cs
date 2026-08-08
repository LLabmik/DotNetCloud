using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Tracks.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintDiscussion_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SprintDiscussions",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintDiscussions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SprintDiscussions_ReviewSessions_ReviewSessionId",
                        column: x => x.ReviewSessionId,
                        principalSchema: "tracks",
                        principalTable: "ReviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SprintDiscussions_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalSchema: "tracks",
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sprint_discussions_review_created",
                schema: "tracks",
                table: "SprintDiscussions",
                columns: new[] { "ReviewSessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_sprint_discussions_sprint_created",
                schema: "tracks",
                table: "SprintDiscussions",
                columns: new[] { "SprintId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SprintDiscussions",
                schema: "tracks");
        }
    }
}
