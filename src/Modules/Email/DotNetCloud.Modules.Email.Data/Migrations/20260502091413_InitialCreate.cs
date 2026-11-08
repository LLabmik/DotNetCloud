using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Email.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderType = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EmailAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    EncryptedCredentialBlob = table.Column<string>(type: "text", nullable: true),
                    SyncStateJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    StopProcessing = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderThreadId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Snippet = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ParticipantsJson = table.Column<string>(type: "jsonb", nullable: false),
                    MessageCount = table.Column<int>(type: "integer", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailThreads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailMailboxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SyncFlags = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMailboxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailMailboxes_EmailAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "EmailAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailRuleActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    TargetValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRuleActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailRuleActions_EmailRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "EmailRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailRuleConditionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchMode = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRuleConditionGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailRuleConditionGroups_EmailRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "EmailRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    MailboxId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    MessageIdHeader = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InReplyTo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    References = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FromJson = table.Column<string>(type: "jsonb", nullable: false),
                    ToJson = table.Column<string>(type: "jsonb", nullable: false),
                    CcJson = table.Column<string>(type: "jsonb", nullable: false),
                    BccJson = table.Column<string>(type: "jsonb", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BodyPreview = table.Column<string>(type: "text", nullable: true),
                    DateReceived = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateSent = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    IsStarred = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    FlagsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailMessages_EmailThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "EmailThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailRuleConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConditionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    Operator = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailRuleConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailRuleConditions_EmailRuleConditionGroups_ConditionGroup~",
                        column: x => x.ConditionGroupId,
                        principalTable: "EmailRuleConditionGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    ContentId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailAttachments_EmailMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "EmailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_email_accounts_email",
                table: "EmailAccounts",
                column: "EmailAddress");

            migrationBuilder.CreateIndex(
                name: "ix_email_accounts_owner_id",
                table: "EmailAccounts",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_email_attachments_message_id",
                table: "EmailAttachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "ix_email_mailboxes_account_id",
                table: "EmailMailboxes",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "ix_email_mailboxes_account_provider",
                table: "EmailMailboxes",
                columns: new[] { "AccountId", "ProviderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_account_id",
                table: "EmailMessages",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_account_provider",
                table: "EmailMessages",
                columns: new[] { "AccountId", "ProviderMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_date_received",
                table: "EmailMessages",
                column: "DateReceived");

            migrationBuilder.CreateIndex(
                name: "ix_email_messages_thread_id",
                table: "EmailMessages",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "ix_email_rule_actions_rule_id",
                table: "EmailRuleActions",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "ix_email_rule_condition_groups_rule_id",
                table: "EmailRuleConditionGroups",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "ix_email_rule_conditions_group_id",
                table: "EmailRuleConditions",
                column: "ConditionGroupId");

            migrationBuilder.CreateIndex(
                name: "ix_email_rules_owner_account",
                table: "EmailRules",
                columns: new[] { "OwnerId", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "ix_email_rules_owner_id",
                table: "EmailRules",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_email_threads_account_id",
                table: "EmailThreads",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "ix_email_threads_account_provider",
                table: "EmailThreads",
                columns: new[] { "AccountId", "ProviderThreadId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_threads_last_message",
                table: "EmailThreads",
                column: "LastMessageAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailAttachments");

            migrationBuilder.DropTable(
                name: "EmailMailboxes");

            migrationBuilder.DropTable(
                name: "EmailRuleActions");

            migrationBuilder.DropTable(
                name: "EmailRuleConditions");

            migrationBuilder.DropTable(
                name: "EmailMessages");

            migrationBuilder.DropTable(
                name: "EmailAccounts");

            migrationBuilder.DropTable(
                name: "EmailRuleConditionGroups");

            migrationBuilder.DropTable(
                name: "EmailThreads");

            migrationBuilder.DropTable(
                name: "EmailRules");
        }
    }
}
