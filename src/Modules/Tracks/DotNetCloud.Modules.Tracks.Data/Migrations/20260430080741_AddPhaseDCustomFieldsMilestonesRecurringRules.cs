using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Tracks.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhaseDCustomFieldsMilestonesRecurringRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MilestoneId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurringRuleId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OptionsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Position = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFields_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Milestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Upcoming"),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Milestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Milestones_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurringRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SwimlaneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TemplateJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NextRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringRules_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringRules_Swimlanes_SwimlaneId",
                        column: x => x.SwimlaneId,
                        principalTable: "Swimlanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemFieldValues_CustomFields_CustomFieldId",
                        column: x => x.CustomFieldId,
                        principalTable: "CustomFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemFieldValues_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_MilestoneId",
                table: "WorkItems",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_RecurringRuleId",
                table: "WorkItems",
                column: "RecurringRuleId");

            migrationBuilder.CreateIndex(
                name: "uq_custom_fields_product_name",
                table: "CustomFields",
                columns: new[] { "ProductId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_milestones_product_title",
                table: "Milestones",
                columns: new[] { "ProductId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recurring_rules_next_run",
                table: "RecurringRules",
                column: "NextRunAt");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringRules_ProductId",
                table: "RecurringRules",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringRules_SwimlaneId",
                table: "RecurringRules",
                column: "SwimlaneId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemFieldValues_CustomFieldId",
                table: "WorkItemFieldValues",
                column: "CustomFieldId");

            migrationBuilder.CreateIndex(
                name: "uq_workitem_fieldvalue_item_field",
                table: "WorkItemFieldValues",
                columns: new[] { "WorkItemId", "CustomFieldId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Milestones_MilestoneId",
                table: "WorkItems",
                column: "MilestoneId",
                principalTable: "Milestones",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_RecurringRules_RecurringRuleId",
                table: "WorkItems",
                column: "RecurringRuleId",
                principalTable: "RecurringRules",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Milestones_MilestoneId",
                table: "WorkItems");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_RecurringRules_RecurringRuleId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "Milestones");

            migrationBuilder.DropTable(
                name: "RecurringRules");

            migrationBuilder.DropTable(
                name: "WorkItemFieldValues");

            migrationBuilder.DropTable(
                name: "CustomFields");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_MilestoneId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_RecurringRuleId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "MilestoneId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "RecurringRuleId",
                table: "WorkItems");
        }
    }
}
