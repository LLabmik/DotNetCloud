using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Contacts.Data.Migrations
{
    /// <inheritdoc />
    public partial class PromoteContactsToRequiredModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.RenameTable(
                name: "ContactShares",
                schema: "contacts",
                newName: "ContactShares",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "Contacts",
                schema: "contacts",
                newName: "Contacts",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "ContactPhones",
                schema: "contacts",
                newName: "ContactPhones",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "ContactGroups",
                schema: "contacts",
                newName: "ContactGroups",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "ContactGroupMembers",
                schema: "contacts",
                newName: "ContactGroupMembers",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "ContactEmails",
                schema: "contacts",
                newName: "ContactEmails",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "ContactCustomFields",
                schema: "contacts",
                newName: "ContactCustomFields",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "ContactAttachments",
                schema: "contacts",
                newName: "ContactAttachments",
                newSchema: "core");

            migrationBuilder.RenameTable(
                name: "ContactAddresses",
                schema: "contacts",
                newName: "ContactAddresses",
                newSchema: "core");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactShares",
                schema: "core",
                newName: "ContactShares",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "Contacts",
                schema: "core",
                newName: "Contacts",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactPhones",
                schema: "core",
                newName: "ContactPhones",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactGroups",
                schema: "core",
                newName: "ContactGroups",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactGroupMembers",
                schema: "core",
                newName: "ContactGroupMembers",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactEmails",
                schema: "core",
                newName: "ContactEmails",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactCustomFields",
                schema: "core",
                newName: "ContactCustomFields",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactAttachments",
                schema: "core",
                newName: "ContactAttachments",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactAddresses",
                schema: "core",
                newName: "ContactAddresses",
                newSchema: "contacts");
        }
    }
}
