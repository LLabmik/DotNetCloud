using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Tracks.Data.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tracks");

            migrationBuilder.CreateTable(
                name: "Products",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubItemsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ETag = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductTemplates",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TracksTeams",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TracksTeams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activities_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRules",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConditionsJson = table.Column<string>(type: "text", nullable: false),
                    ActionsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationRules_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomFields",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Position = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFields_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomViews",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FilterJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SortJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    GroupBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Layout = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomViews_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Goals",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ParentGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetValue = table.Column<double>(type: "float", nullable: true),
                    CurrentValue = table.Column<double>(type: "float", nullable: true),
                    ProgressType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Goals_Goals_ParentGoalId",
                        column: x => x.ParentGoalId,
                        principalSchema: "tracks",
                        principalTable: "Goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Goals_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuestUsers",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InviteToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestUsers_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTemplates",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TitlePattern = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LabelIdsJson = table.Column<string>(type: "text", nullable: true),
                    ChecklistsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemTemplates_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Labels",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Labels_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Milestones",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Upcoming"),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Milestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Milestones_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductMembers",
                schema: "tracks",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMembers", x => new { x.ProductId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ProductMembers_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Swimlanes",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContainerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContainerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Position = table.Column<double>(type: "float", nullable: false),
                    CardLimit = table.Column<int>(type: "int", nullable: true),
                    IsDone = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Swimlanes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Swimlanes_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WebhookSubscriptions",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Secret = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventsJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastDeliveryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedDeliveryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookSubscriptions_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamRoles",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamRoles_TracksTeams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "tracks",
                        principalTable: "TracksTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurringRules",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SwimlaneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TemplateJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NextRunAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringRules_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringRules_Swimlanes_SwimlaneId",
                        column: x => x.SwimlaneId,
                        principalSchema: "tracks",
                        principalTable: "Swimlanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SwimlaneTransitionRules",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromSwimlaneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToSwimlaneId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsAllowed = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SwimlaneTransitionRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SwimlaneTransitionRules_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SwimlaneTransitionRules_Swimlanes_FromSwimlaneId",
                        column: x => x.FromSwimlaneId,
                        principalSchema: "tracks",
                        principalTable: "Swimlanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SwimlaneTransitionRules_Swimlanes_ToSwimlaneId",
                        column: x => x.ToSwimlaneId,
                        principalSchema: "tracks",
                        principalTable: "Swimlanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeliveries",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "text", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookDeliveries_WebhookSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "tracks",
                        principalTable: "WebhookSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItems",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentWorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SwimlaneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ItemNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Position = table.Column<double>(type: "float", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoryPoints = table.Column<int>(type: "int", nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ETag = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    MilestoneId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RecurringRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItems_Milestones_MilestoneId",
                        column: x => x.MilestoneId,
                        principalSchema: "tracks",
                        principalTable: "Milestones",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "tracks",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItems_RecurringRules_RecurringRuleId",
                        column: x => x.RecurringRuleId,
                        principalSchema: "tracks",
                        principalTable: "RecurringRules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkItems_Swimlanes_SwimlaneId",
                        column: x => x.SwimlaneId,
                        principalSchema: "tracks",
                        principalTable: "Swimlanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkItems_WorkItems_ParentWorkItemId",
                        column: x => x.ParentWorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Checklists",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Position = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Checklists_WorkItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoalWorkItems",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalWorkItems_Goals_GoalId",
                        column: x => x.GoalId,
                        principalSchema: "tracks",
                        principalTable: "Goals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoalWorkItems_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuestPermissions",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permission = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestPermissions_GuestUsers_GuestUserId",
                        column: x => x.GuestUserId,
                        principalSchema: "tracks",
                        principalTable: "GuestUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuestPermissions_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewSessions",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EpicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSessions_WorkItems_CurrentItemId",
                        column: x => x.CurrentItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReviewSessions_WorkItems_EpicId",
                        column: x => x.EpicId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sprints",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EpicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Goal = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetStoryPoints = table.Column<int>(type: "int", nullable: true),
                    DurationWeeks = table.Column<int>(type: "int", nullable: true),
                    PlannedOrder = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sprints_WorkItems_EpicId",
                        column: x => x.EpicId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimeEntries",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeEntries_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemAssignments",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemAssignments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemAttachments",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    MimeType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemAttachments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemComments",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsEdited = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemComments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemDependencies",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DependsOnWorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemDependencies_WorkItems_DependsOnWorkItemId",
                        column: x => x.DependsOnWorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemDependencies_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemFieldValues",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemFieldValues_CustomFields_CustomFieldId",
                        column: x => x.CustomFieldId,
                        principalSchema: "tracks",
                        principalTable: "CustomFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemFieldValues_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemLabels",
                schema: "tracks",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LabelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemLabels", x => new { x.WorkItemId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_WorkItemLabels_Labels_LabelId",
                        column: x => x.LabelId,
                        principalSchema: "tracks",
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemLabels_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemShareLinks",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Permission = table.Column<int>(type: "int", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemShareLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemShareLinks_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemWatchers",
                schema: "tracks",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscribedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemWatchers", x => new { x.WorkItemId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkItemWatchers_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistItems",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    Position = table.Column<double>(type: "float", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistItems_Checklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalSchema: "tracks",
                        principalTable: "Checklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PokerSessions",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EpicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CustomScaleValues = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AcceptedEstimate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Round = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ReviewSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokerSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PokerSessions_ReviewSessions_ReviewSessionId",
                        column: x => x.ReviewSessionId,
                        principalSchema: "tracks",
                        principalTable: "ReviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PokerSessions_WorkItems_EpicId",
                        column: x => x.EpicId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PokerSessions_WorkItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewSessionParticipants",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsConnected = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewSessionParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSessionParticipants_ReviewSessions_ReviewSessionId",
                        column: x => x.ReviewSessionId,
                        principalSchema: "tracks",
                        principalTable: "ReviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SprintItems",
                schema: "tracks",
                columns: table => new
                {
                    SprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintItems", x => new { x.SprintId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_SprintItems_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalSchema: "tracks",
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SprintItems_WorkItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "tracks",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommentReactions",
                schema: "tracks",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentReactions", x => new { x.CommentId, x.UserId, x.Emoji });
                    table.ForeignKey(
                        name: "FK_CommentReactions_WorkItemComments_CommentId",
                        column: x => x.CommentId,
                        principalSchema: "tracks",
                        principalTable: "WorkItemComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PokerVotes",
                schema: "tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Estimate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    VotedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokerVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PokerVotes_PokerSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "tracks",
                        principalTable: "PokerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activities_entity",
                schema: "tracks",
                table: "Activities",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "ix_activities_product_created",
                schema: "tracks",
                table: "Activities",
                columns: new[] { "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_activities_user_id",
                schema: "tracks",
                table: "Activities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_automation_rules_product_id",
                schema: "tracks",
                table: "AutomationRules",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ix_checklist_items_assigned_to",
                schema: "tracks",
                table: "ChecklistItems",
                column: "AssignedToUserId",
                filter: "\"AssignedToUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_checklist_items_checklist_position",
                schema: "tracks",
                table: "ChecklistItems",
                columns: new[] { "ChecklistId", "Position" });

            migrationBuilder.CreateIndex(
                name: "ix_checklists_item_position",
                schema: "tracks",
                table: "Checklists",
                columns: new[] { "ItemId", "Position" });

            migrationBuilder.CreateIndex(
                name: "ix_comment_reactions_comment",
                schema: "tracks",
                table: "CommentReactions",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "uq_custom_fields_product_name",
                schema: "tracks",
                table: "CustomFields",
                columns: new[] { "ProductId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomViews_ProductId",
                schema: "tracks",
                table: "CustomViews",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomViews_ProductId_UserId_Name",
                schema: "tracks",
                table: "CustomViews",
                columns: new[] { "ProductId", "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goals_parent_goal_id",
                schema: "tracks",
                table: "Goals",
                column: "ParentGoalId");

            migrationBuilder.CreateIndex(
                name: "ix_goals_product_id",
                schema: "tracks",
                table: "Goals",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalWorkItems_WorkItemId",
                schema: "tracks",
                table: "GoalWorkItems",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "uq_goal_workitem",
                schema: "tracks",
                table: "GoalWorkItems",
                columns: new[] { "GoalId", "WorkItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_guest_permissions_guest_work_item",
                schema: "tracks",
                table: "GuestPermissions",
                columns: new[] { "GuestUserId", "WorkItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestPermissions_WorkItemId",
                schema: "tracks",
                table: "GuestPermissions",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "ix_guest_users_email",
                schema: "tracks",
                table: "GuestUsers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "ix_guest_users_invite_token",
                schema: "tracks",
                table: "GuestUsers",
                column: "InviteToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_guest_users_product",
                schema: "tracks",
                table: "GuestUsers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ix_item_templates_product_id",
                schema: "tracks",
                table: "ItemTemplates",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "uq_labels_product_title",
                schema: "tracks",
                table: "Labels",
                columns: new[] { "ProductId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_milestones_product_title",
                schema: "tracks",
                table: "Milestones",
                columns: new[] { "ProductId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_poker_sessions_created_by",
                schema: "tracks",
                table: "PokerSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_poker_sessions_epic_status",
                schema: "tracks",
                table: "PokerSessions",
                columns: new[] { "EpicId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_poker_sessions_item_status",
                schema: "tracks",
                table: "PokerSessions",
                columns: new[] { "ItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_poker_sessions_review_session",
                schema: "tracks",
                table: "PokerSessions",
                column: "ReviewSessionId",
                filter: "\"ReviewSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_poker_votes_session_user_round",
                schema: "tracks",
                table: "PokerVotes",
                columns: new[] { "SessionId", "UserId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_poker_votes_user",
                schema: "tracks",
                table: "PokerVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_product_members_product_role",
                schema: "tracks",
                table: "ProductMembers",
                columns: new[] { "ProductId", "Role" });

            migrationBuilder.CreateIndex(
                name: "ix_product_members_user_id",
                schema: "tracks",
                table: "ProductMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_products_created_at",
                schema: "tracks",
                table: "Products",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_products_is_archived",
                schema: "tracks",
                table: "Products",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "ix_products_is_deleted",
                schema: "tracks",
                table: "Products",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_products_organization_id",
                schema: "tracks",
                table: "Products",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "ix_products_owner_id",
                schema: "tracks",
                table: "Products",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_product_templates_category",
                schema: "tracks",
                table: "ProductTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "ix_product_templates_is_built_in",
                schema: "tracks",
                table: "ProductTemplates",
                column: "IsBuiltIn");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_rules_next_run",
                schema: "tracks",
                table: "RecurringRules",
                column: "NextRunAt");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringRules_ProductId",
                schema: "tracks",
                table: "RecurringRules",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringRules_SwimlaneId",
                schema: "tracks",
                table: "RecurringRules",
                column: "SwimlaneId");

            migrationBuilder.CreateIndex(
                name: "ix_review_session_participants_session_user",
                schema: "tracks",
                table: "ReviewSessionParticipants",
                columns: new[] { "ReviewSessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_session_participants_user_id",
                schema: "tracks",
                table: "ReviewSessionParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_epic_status",
                schema: "tracks",
                table: "ReviewSessions",
                columns: new[] { "EpicId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_host_user_id",
                schema: "tracks",
                table: "ReviewSessions",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSessions_CurrentItemId",
                schema: "tracks",
                table: "ReviewSessions",
                column: "CurrentItemId");

            migrationBuilder.CreateIndex(
                name: "ix_sprint_items_item_id",
                schema: "tracks",
                table: "SprintItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "ix_sprints_epic_status",
                schema: "tracks",
                table: "Sprints",
                columns: new[] { "EpicId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_sprints_start_date",
                schema: "tracks",
                table: "Sprints",
                column: "StartDate",
                filter: "\"StartDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_swimlanes_container_position",
                schema: "tracks",
                table: "Swimlanes",
                columns: new[] { "ContainerType", "ContainerId", "Position" });

            migrationBuilder.CreateIndex(
                name: "ix_swimlanes_is_archived",
                schema: "tracks",
                table: "Swimlanes",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Swimlanes_ProductId",
                schema: "tracks",
                table: "Swimlanes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ix_swimlane_transition_rules_from_to",
                schema: "tracks",
                table: "SwimlaneTransitionRules",
                columns: new[] { "FromSwimlaneId", "ToSwimlaneId" });

            migrationBuilder.CreateIndex(
                name: "ix_swimlane_transition_rules_product_from_to",
                schema: "tracks",
                table: "SwimlaneTransitionRules",
                columns: new[] { "ProductId", "FromSwimlaneId", "ToSwimlaneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SwimlaneTransitionRules_ToSwimlaneId",
                schema: "tracks",
                table: "SwimlaneTransitionRules",
                column: "ToSwimlaneId");

            migrationBuilder.CreateIndex(
                name: "ix_team_roles_team_id",
                schema: "tracks",
                table: "TeamRoles",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "ix_team_roles_user_id",
                schema: "tracks",
                table: "TeamRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "uq_team_roles_team_user",
                schema: "tracks",
                table: "TeamRoles",
                columns: new[] { "TeamId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_start_time",
                schema: "tracks",
                table: "TimeEntries",
                column: "StartTime",
                filter: "\"StartTime\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_user_id",
                schema: "tracks",
                table: "TimeEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_work_item_id",
                schema: "tracks",
                table: "TimeEntries",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_work_item_user",
                schema: "tracks",
                table: "TimeEntries",
                columns: new[] { "WorkItemId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "ix_tracks_teams_name",
                schema: "tracks",
                table: "TracksTeams",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_created",
                schema: "tracks",
                table: "WebhookDeliveries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_subscription",
                schema: "tracks",
                table: "WebhookDeliveries",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_subscriptions_active",
                schema: "tracks",
                table: "WebhookSubscriptions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_subscriptions_product",
                schema: "tracks",
                table: "WebhookSubscriptions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_assignments_user_id",
                schema: "tracks",
                table: "WorkItemAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "uq_work_item_assignments_item_user",
                schema: "tracks",
                table: "WorkItemAssignments",
                columns: new[] { "WorkItemId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_item_attachments_file_node",
                schema: "tracks",
                table: "WorkItemAttachments",
                column: "FileNodeId",
                filter: "\"FileNodeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_attachments_work_item",
                schema: "tracks",
                table: "WorkItemAttachments",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_comments_created_at",
                schema: "tracks",
                table: "WorkItemComments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_comments_user",
                schema: "tracks",
                table: "WorkItemComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_comments_work_item",
                schema: "tracks",
                table: "WorkItemComments",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_dependencies_depends_on",
                schema: "tracks",
                table: "WorkItemDependencies",
                column: "DependsOnWorkItemId");

            migrationBuilder.CreateIndex(
                name: "uq_work_item_dependencies_item_depends_type",
                schema: "tracks",
                table: "WorkItemDependencies",
                columns: new[] { "WorkItemId", "DependsOnWorkItemId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemFieldValues_CustomFieldId",
                schema: "tracks",
                table: "WorkItemFieldValues",
                column: "CustomFieldId");

            migrationBuilder.CreateIndex(
                name: "uq_workitem_fieldvalue_item_field",
                schema: "tracks",
                table: "WorkItemFieldValues",
                columns: new[] { "WorkItemId", "CustomFieldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLabels_LabelId",
                schema: "tracks",
                table: "WorkItemLabels",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_created_at",
                schema: "tracks",
                table: "WorkItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_created_by",
                schema: "tracks",
                table: "WorkItems",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_due_date",
                schema: "tracks",
                table: "WorkItems",
                column: "DueDate",
                filter: "\"DueDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_is_archived",
                schema: "tracks",
                table: "WorkItems",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_is_deleted",
                schema: "tracks",
                table: "WorkItems",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_parent",
                schema: "tracks",
                table: "WorkItems",
                column: "ParentWorkItemId");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_priority",
                schema: "tracks",
                table: "WorkItems",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_product_type",
                schema: "tracks",
                table: "WorkItems",
                columns: new[] { "ProductId", "Type" });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_start_date",
                schema: "tracks",
                table: "WorkItems",
                column: "StartDate",
                filter: "\"StartDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_swimlane_position",
                schema: "tracks",
                table: "WorkItems",
                columns: new[] { "SwimlaneId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_MilestoneId",
                schema: "tracks",
                table: "WorkItems",
                column: "MilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_RecurringRuleId",
                schema: "tracks",
                table: "WorkItems",
                column: "RecurringRuleId");

            migrationBuilder.CreateIndex(
                name: "uq_work_items_product_number",
                schema: "tracks",
                table: "WorkItems",
                columns: new[] { "ProductId", "ItemNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_item_share_links_active",
                schema: "tracks",
                table: "WorkItemShareLinks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_share_links_token",
                schema: "tracks",
                table: "WorkItemShareLinks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_item_share_links_work_item",
                schema: "tracks",
                table: "WorkItemShareLinks",
                column: "WorkItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "AutomationRules",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "ChecklistItems",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "CommentReactions",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "CustomViews",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "GoalWorkItems",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "GuestPermissions",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "ItemTemplates",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "PokerVotes",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "ProductMembers",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "ProductTemplates",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "ReviewSessionParticipants",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "SprintItems",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "SwimlaneTransitionRules",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "TeamRoles",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "TimeEntries",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WebhookDeliveries",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WorkItemAssignments",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WorkItemAttachments",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WorkItemDependencies",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WorkItemFieldValues",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WorkItemLabels",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WorkItemShareLinks",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WorkItemWatchers",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "Checklists",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WorkItemComments",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "Goals",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "GuestUsers",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "PokerSessions",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "Sprints",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "TracksTeams",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WebhookSubscriptions",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "CustomFields",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "Labels",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "ReviewSessions",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "WorkItems",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "Milestones",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "RecurringRules",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "Swimlanes",
                schema: "tracks");

            migrationBuilder.DropTable(
                name: "Products",
                schema: "tracks");
        }
    }
}
