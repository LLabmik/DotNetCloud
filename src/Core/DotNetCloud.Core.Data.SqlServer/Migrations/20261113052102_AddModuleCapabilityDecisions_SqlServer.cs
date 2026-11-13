using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleCapabilityDecisions_SqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAtUtc",
                table: "ModuleCapabilityGrants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedByUserId",
                table: "ModuleCapabilityGrants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ModuleCapabilityGrants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Granted");

            migrationBuilder.CreateIndex(
                name: "ix_module_capability_grants_module_id_status",
                table: "ModuleCapabilityGrants",
                columns: new[] { "ModuleId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_module_capability_grants_rejected_by_user_id",
                table: "ModuleCapabilityGrants",
                column: "RejectedByUserId");

            migrationBuilder.AddForeignKey(
                name: "fk_module_capability_grants_rejected_by_user_id",
                table: "ModuleCapabilityGrants",
                column: "RejectedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_module_capability_grants_rejected_by_user_id",
                table: "ModuleCapabilityGrants");

            migrationBuilder.DropIndex(
                name: "ix_module_capability_grants_module_id_status",
                table: "ModuleCapabilityGrants");

            migrationBuilder.DropIndex(
                name: "ix_module_capability_grants_rejected_by_user_id",
                table: "ModuleCapabilityGrants");

            migrationBuilder.DropColumn(
                name: "RejectedAtUtc",
                table: "ModuleCapabilityGrants");

            migrationBuilder.DropColumn(
                name: "RejectedByUserId",
                table: "ModuleCapabilityGrants");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ModuleCapabilityGrants");
        }
    }
}
