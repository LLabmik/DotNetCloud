using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Contacts.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddSequentialGuidDefaults_SqlServer : Migration
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

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "ContactShares",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "Contacts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "ContactPhones",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "ContactGroups",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "ContactEmails",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "ContactCustomFields",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "ContactAttachments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "core",
                table: "ContactAddresses",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
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

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "contacts",
                table: "ContactShares",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "contacts",
                table: "Contacts",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "contacts",
                table: "ContactPhones",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "contacts",
                table: "ContactGroups",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "contacts",
                table: "ContactEmails",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "contacts",
                table: "ContactCustomFields",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "contacts",
                table: "ContactAttachments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "contacts",
                table: "ContactAddresses",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");
        }
    }
}
