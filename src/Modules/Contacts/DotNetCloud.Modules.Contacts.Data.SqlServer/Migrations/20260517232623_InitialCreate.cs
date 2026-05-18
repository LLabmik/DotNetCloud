using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Contacts.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "contacts");

            migrationBuilder.CreateTable(
                name: "ContactGroups",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Prefix = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Suffix = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PhoneticName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Nickname = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Organization = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Department = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    Birthday = table.Column<DateOnly>(type: "date", nullable: true),
                    Anniversary = table.Column<DateOnly>(type: "date", nullable: true),
                    WebsiteUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ETag = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContactAddresses",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Street = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactAddresses_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "contacts",
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactAttachments",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsAvatar = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactAttachments_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "contacts",
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactCustomFields",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactCustomFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactCustomFields_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "contacts",
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactEmails",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactEmails_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "contacts",
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactGroupMembers",
                schema: "contacts",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactGroupMembers", x => new { x.GroupId, x.ContactId });
                    table.ForeignKey(
                        name: "FK_ContactGroupMembers_ContactGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "contacts",
                        principalTable: "ContactGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContactGroupMembers_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "contacts",
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactPhones",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactPhones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactPhones_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "contacts",
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContactShares",
                schema: "contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SharedWithUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SharedWithTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Permission = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactShares_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalSchema: "contacts",
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contact_addresses_contact_id",
                schema: "contacts",
                table: "ContactAddresses",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "ix_contact_attachments_contact_avatar",
                schema: "contacts",
                table: "ContactAttachments",
                columns: new[] { "ContactId", "IsAvatar" });

            migrationBuilder.CreateIndex(
                name: "ix_contact_attachments_contact_id",
                schema: "contacts",
                table: "ContactAttachments",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "ix_contact_custom_fields_contact_id",
                schema: "contacts",
                table: "ContactCustomFields",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "uq_contact_custom_fields_contact_key",
                schema: "contacts",
                table: "ContactCustomFields",
                columns: new[] { "ContactId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contact_emails_address",
                schema: "contacts",
                table: "ContactEmails",
                column: "Address");

            migrationBuilder.CreateIndex(
                name: "ix_contact_emails_contact_id",
                schema: "contacts",
                table: "ContactEmails",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "ix_contact_group_members_contact_id",
                schema: "contacts",
                table: "ContactGroupMembers",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "ix_contact_groups_owner_id",
                schema: "contacts",
                table: "ContactGroups",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "uq_contact_groups_owner_name",
                schema: "contacts",
                table: "ContactGroups",
                columns: new[] { "OwnerId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contact_phones_contact_id",
                schema: "contacts",
                table: "ContactPhones",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "ix_contacts_display_name",
                schema: "contacts",
                table: "Contacts",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "ix_contacts_is_deleted",
                schema: "contacts",
                table: "Contacts",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_contacts_owner_display_name",
                schema: "contacts",
                table: "Contacts",
                columns: new[] { "OwnerId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "ix_contacts_owner_id",
                schema: "contacts",
                table: "Contacts",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_contacts_owner_name",
                schema: "contacts",
                table: "Contacts",
                columns: new[] { "OwnerId", "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "ix_contacts_type",
                schema: "contacts",
                table: "Contacts",
                column: "ContactType");

            migrationBuilder.CreateIndex(
                name: "ix_contact_shares_contact_id",
                schema: "contacts",
                table: "ContactShares",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "ix_contact_shares_shared_with_team",
                schema: "contacts",
                table: "ContactShares",
                column: "SharedWithTeamId");

            migrationBuilder.CreateIndex(
                name: "ix_contact_shares_shared_with_user",
                schema: "contacts",
                table: "ContactShares",
                column: "SharedWithUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactAddresses",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "ContactAttachments",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "ContactCustomFields",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "ContactEmails",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "ContactGroupMembers",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "ContactPhones",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "ContactShares",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "ContactGroups",
                schema: "contacts");

            migrationBuilder.DropTable(
                name: "Contacts",
                schema: "contacts");
        }
    }
}
