using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Email.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixEmailMessageUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_email_messages_account_provider",
                schema: "email",
                table: "EmailMessages");

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_account_mailbox_provider",
                schema: "email",
                table: "EmailMessages",
                columns: new[] { "AccountId", "MailboxId", "ProviderMessageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_email_messages_account_mailbox_provider",
                schema: "email",
                table: "EmailMessages");

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_account_provider",
                schema: "email",
                table: "EmailMessages",
                columns: new[] { "AccountId", "ProviderMessageId" },
                unique: true);
        }
    }
}
