using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Tracks.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tracks");

            migrationBuilder.CreateTable(
                name: "CustomViews",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FilterJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SortJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    GroupBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Layout = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomViews_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemWatchers",
                schema: "tracks",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscribedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemWatchers", x => new { x.WorkItemId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkItemWatchers_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomViews_ProductId",
                schema: "tracks",
                table: "CustomViews",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomViews_ProductId_UserId_Name",
                schema: "tracks",
                table: "CustomViews",
                columns: new[] { "ProductId", "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomViews",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WorkItemWatchers",
                schema: "tracks");
        }
    }
}
