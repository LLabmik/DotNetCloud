using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Contacts.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactShares",
                newName: "ContactShares",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "Contacts",
                newName: "Contacts",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactPhones",
                newName: "ContactPhones",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactGroups",
                newName: "ContactGroups",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactGroupMembers",
                newName: "ContactGroupMembers",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactEmails",
                newName: "ContactEmails",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactCustomFields",
                newName: "ContactCustomFields",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactAttachments",
                newName: "ContactAttachments",
                newSchema: "contacts");

            migrationBuilder.RenameTable(
                name: "ContactAddresses",
                newName: "ContactAddresses",
                newSchema: "contacts");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                schema: "contacts",
                table: "Contacts",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldMaxLength: 10000,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "ContactShares",
                schema: "contacts",
                newName: "ContactShares");

            migrationBuilder.RenameTable(
                name: "Contacts",
                schema: "contacts",
                newName: "Contacts");

            migrationBuilder.RenameTable(
                name: "ContactPhones",
                schema: "contacts",
                newName: "ContactPhones");

            migrationBuilder.RenameTable(
                name: "ContactGroups",
                schema: "contacts",
                newName: "ContactGroups");

            migrationBuilder.RenameTable(
                name: "ContactGroupMembers",
                schema: "contacts",
                newName: "ContactGroupMembers");

            migrationBuilder.RenameTable(
                name: "ContactEmails",
                schema: "contacts",
                newName: "ContactEmails");

            migrationBuilder.RenameTable(
                name: "ContactCustomFields",
                schema: "contacts",
                newName: "ContactCustomFields");

            migrationBuilder.RenameTable(
                name: "ContactAttachments",
                schema: "contacts",
                newName: "ContactAttachments");

            migrationBuilder.RenameTable(
                name: "ContactAddresses",
                schema: "contacts",
                newName: "ContactAddresses");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "Contacts",
                type: "text",
                maxLength: 10000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10000)",
                oldMaxLength: 10000,
                oldNullable: true);
        }
    }
}
