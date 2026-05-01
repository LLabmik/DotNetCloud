using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Tracks.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryFilters_ProductsWorkItemsComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SwimlaneTransitionRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromSwimlaneId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToSwimlaneId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAllowed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwimlaneTransitionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SwimlaneTransitionRules_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SwimlaneTransitionRules_Swimlanes_FromSwimlaneId",
                        column: x => x.FromSwimlaneId,
                        principalTable: "Swimlanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwimlaneTransitionRules_Swimlanes_ToSwimlaneId",
                        column: x => x.ToSwimlaneId,
                        principalTable: "Swimlanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_swimlane_transition_rules_from_to",
                table: "SwimlaneTransitionRules",
                columns: new[] { "FromSwimlaneId", "ToSwimlaneId" });

            migrationBuilder.CreateIndex(
                name: "ix_swimlane_transition_rules_product_from_to",
                table: "SwimlaneTransitionRules",
                columns: new[] { "ProductId", "FromSwimlaneId", "ToSwimlaneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwimlaneTransitionRules_ToSwimlaneId",
                table: "SwimlaneTransitionRules",
                column: "ToSwimlaneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SwimlaneTransitionRules");
        }
    }
}
