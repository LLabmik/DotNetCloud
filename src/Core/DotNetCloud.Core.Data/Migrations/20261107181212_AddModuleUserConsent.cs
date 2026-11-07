using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleUserConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModuleUserConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleUserConsents", x => x.Id);
                    table.ForeignKey(
                        name: "fk_module_user_consents_granted_by_user_id",
                        column: x => x.GrantedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_module_user_consents_module_id",
                        column: x => x.ModuleId,
                        principalTable: "InstalledModules",
                        principalColumn: "ModuleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_module_user_consents_user_id",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_module_user_consents_module_id",
                table: "ModuleUserConsents",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "ix_module_user_consents_user_id",
                table: "ModuleUserConsents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleUserConsents_GrantedByUserId",
                table: "ModuleUserConsents",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "uq_module_user_consents_module_id_user_id_scope",
                table: "ModuleUserConsents",
                columns: new[] { "ModuleId", "UserId", "Scope" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModuleUserConsents");
        }
    }
}
