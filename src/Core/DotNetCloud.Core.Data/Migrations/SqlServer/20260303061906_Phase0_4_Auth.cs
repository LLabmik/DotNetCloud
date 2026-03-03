using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Core.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class Phase0_4_Auth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.CreateTable(
                name: "FidoCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
                    PublicKey = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
                    SignatureCounter = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    DeviceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FidoCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FidoCredentials_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "open_iddict_applications",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "text", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedirectUris = table.Column<string>(type: "text", nullable: true),
                    Requirements = table.Column<string>(type: "text", nullable: true),
                    Settings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_open_iddict_applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "open_iddict_scopes",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Descriptions = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Resources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_open_iddict_scopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserBackupCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBackupCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserBackupCodes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "open_iddict_authorizations",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_open_iddict_authorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_open_iddict_authorizations_open_iddict_applications_Applica~",
                        column: x => x.ApplicationId,
                        principalSchema: "core",
                        principalTable: "open_iddict_applications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "open_iddict_tokens",
                schema: "core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_open_iddict_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_open_iddict_tokens_open_iddict_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "core",
                        principalTable: "open_iddict_applications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_open_iddict_tokens_open_iddict_authorizations_Authorization~",
                        column: x => x.AuthorizationId,
                        principalSchema: "core",
                        principalTable: "open_iddict_authorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FidoCredentials_CredentialId",
                table: "FidoCredentials",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FidoCredentials_UserId",
                table: "FidoCredentials",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_open_iddict_applications_ClientId",
                schema: "core",
                table: "open_iddict_applications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_open_iddict_authorizations_ApplicationId_Status_Subject_Type",
                schema: "core",
                table: "open_iddict_authorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_open_iddict_scopes_Name",
                schema: "core",
                table: "open_iddict_scopes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_open_iddict_tokens_ApplicationId_Status_Subject_Type",
                schema: "core",
                table: "open_iddict_tokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_open_iddict_tokens_AuthorizationId",
                schema: "core",
                table: "open_iddict_tokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_open_iddict_tokens_ReferenceId",
                schema: "core",
                table: "open_iddict_tokens",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserBackupCodes_UserId",
                table: "UserBackupCodes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserBackupCodes_UserId_IsUsed",
                table: "UserBackupCodes",
                columns: new[] { "UserId", "IsUsed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FidoCredentials");

            migrationBuilder.DropTable(
                name: "open_iddict_scopes",
                schema: "core");

            migrationBuilder.DropTable(
                name: "open_iddict_tokens",
                schema: "core");

            migrationBuilder.DropTable(
                name: "UserBackupCodes");

            migrationBuilder.DropTable(
                name: "open_iddict_authorizations",
                schema: "core");

            migrationBuilder.DropTable(
                name: "open_iddict_applications",
                schema: "core");
        }
    }
}
