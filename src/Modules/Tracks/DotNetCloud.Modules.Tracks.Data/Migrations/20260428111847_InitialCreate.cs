using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Tracks.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubItemsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ETag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activities_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TitlePattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LabelIdsJson = table.Column<string>(type: "text", nullable: true),
                    ChecklistsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItemTemplates_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Labels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Labels_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductMembers",
                columns: table => new
                {
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMembers", x => new { x.ProductId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ProductMembers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Swimlanes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ContainerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Position = table.Column<double>(type: "double precision", nullable: false),
                    CardLimit = table.Column<int>(type: "integer", nullable: true),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Swimlanes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Swimlanes_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentWorkItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SwimlaneId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Position = table.Column<double>(type: "double precision", nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StoryPoints = table.Column<int>(type: "integer", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ETag = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItems_Swimlanes_SwimlaneId",
                        column: x => x.SwimlaneId,
                        principalTable: "Swimlanes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkItems_WorkItems_ParentWorkItemId",
                        column: x => x.ParentWorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Checklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Position = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Checklists_WorkItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EpicId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSessions_WorkItems_CurrentItemId",
                        column: x => x.CurrentItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReviewSessions_WorkItems_EpicId",
                        column: x => x.EpicId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EpicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Goal = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TargetStoryPoints = table.Column<int>(type: "integer", nullable: true),
                    DurationWeeks = table.Column<int>(type: "integer", nullable: true),
                    PlannedOrder = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sprints_WorkItems_EpicId",
                        column: x => x.EpicId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeEntries_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemAssignments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    MimeType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemAttachments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemComments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnWorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemDependencies_WorkItems_DependsOnWorkItemId",
                        column: x => x.DependsOnWorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemDependencies_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemLabels",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemLabels", x => new { x.WorkItemId, x.LabelId });
                    table.ForeignKey(
                        name: "FK_WorkItemLabels_Labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "Labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemLabels_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<double>(type: "double precision", nullable: false),
                    AssignedToUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistItems_Checklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalTable: "Checklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PokerSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EpicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomScaleValues = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AcceptedEstimate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Round = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ReviewSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokerSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PokerSessions_ReviewSessions_ReviewSessionId",
                        column: x => x.ReviewSessionId,
                        principalTable: "ReviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PokerSessions_WorkItems_EpicId",
                        column: x => x.EpicId,
                        principalTable: "WorkItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PokerSessions_WorkItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewSessionParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsConnected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewSessionParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewSessionParticipants_ReviewSessions_ReviewSessionId",
                        column: x => x.ReviewSessionId,
                        principalTable: "ReviewSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SprintItems",
                columns: table => new
                {
                    SprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SprintItems", x => new { x.SprintId, x.ItemId });
                    table.ForeignKey(
                        name: "FK_SprintItems_Sprints_SprintId",
                        column: x => x.SprintId,
                        principalTable: "Sprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SprintItems_WorkItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PokerVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Estimate = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    VotedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PokerVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PokerVotes_PokerSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PokerSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activities_entity",
                table: "Activities",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "ix_activities_product_created",
                table: "Activities",
                columns: new[] { "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_activities_user_id",
                table: "Activities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_checklist_items_assigned_to",
                table: "ChecklistItems",
                column: "AssignedToUserId",
                filter: "\"AssignedToUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_checklist_items_checklist_position",
                table: "ChecklistItems",
                columns: new[] { "ChecklistId", "Position" });

            migrationBuilder.CreateIndex(
                name: "ix_checklists_item_position",
                table: "Checklists",
                columns: new[] { "ItemId", "Position" });

            migrationBuilder.CreateIndex(
                name: "ix_item_templates_product_id",
                table: "ItemTemplates",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "uq_labels_product_title",
                table: "Labels",
                columns: new[] { "ProductId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_poker_sessions_created_by",
                table: "PokerSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_poker_sessions_epic_status",
                table: "PokerSessions",
                columns: new[] { "EpicId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_poker_sessions_item_status",
                table: "PokerSessions",
                columns: new[] { "ItemId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_poker_sessions_review_session",
                table: "PokerSessions",
                column: "ReviewSessionId",
                filter: "\"ReviewSessionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_poker_votes_session_user_round",
                table: "PokerVotes",
                columns: new[] { "SessionId", "UserId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_poker_votes_user",
                table: "PokerVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_product_members_product_role",
                table: "ProductMembers",
                columns: new[] { "ProductId", "Role" });

            migrationBuilder.CreateIndex(
                name: "ix_product_members_user_id",
                table: "ProductMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_products_created_at",
                table: "Products",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_products_is_archived",
                table: "Products",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "ix_products_is_deleted",
                table: "Products",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_products_organization_id",
                table: "Products",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "ix_products_owner_id",
                table: "Products",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_product_templates_category",
                table: "ProductTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "ix_product_templates_is_built_in",
                table: "ProductTemplates",
                column: "IsBuiltIn");

            migrationBuilder.CreateIndex(
                name: "ix_review_session_participants_session_user",
                table: "ReviewSessionParticipants",
                columns: new[] { "ReviewSessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_session_participants_user_id",
                table: "ReviewSessionParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_epic_status",
                table: "ReviewSessions",
                columns: new[] { "EpicId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_host_user_id",
                table: "ReviewSessions",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewSessions_CurrentItemId",
                table: "ReviewSessions",
                column: "CurrentItemId");

            migrationBuilder.CreateIndex(
                name: "ix_sprint_items_item_id",
                table: "SprintItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "ix_sprints_epic_status",
                table: "Sprints",
                columns: new[] { "EpicId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_sprints_start_date",
                table: "Sprints",
                column: "StartDate",
                filter: "\"StartDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_swimlanes_container_position",
                table: "Swimlanes",
                columns: new[] { "ContainerType", "ContainerId", "Position" });

            migrationBuilder.CreateIndex(
                name: "ix_swimlanes_is_archived",
                table: "Swimlanes",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Swimlanes_ProductId",
                table: "Swimlanes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "ix_team_roles_team_id",
                table: "TeamRoles",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "ix_team_roles_user_id",
                table: "TeamRoles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "uq_team_roles_team_user",
                table: "TeamRoles",
                columns: new[] { "TeamId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_start_time",
                table: "TimeEntries",
                column: "StartTime",
                filter: "\"StartTime\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_user_id",
                table: "TimeEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_work_item_id",
                table: "TimeEntries",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "ix_time_entries_work_item_user",
                table: "TimeEntries",
                columns: new[] { "WorkItemId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "ix_work_item_assignments_user_id",
                table: "WorkItemAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "uq_work_item_assignments_item_user",
                table: "WorkItemAssignments",
                columns: new[] { "WorkItemId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_item_attachments_file_node",
                table: "WorkItemAttachments",
                column: "FileNodeId",
                filter: "\"FileNodeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_attachments_work_item",
                table: "WorkItemAttachments",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_comments_created_at",
                table: "WorkItemComments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_comments_user",
                table: "WorkItemComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_comments_work_item",
                table: "WorkItemComments",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "ix_work_item_dependencies_depends_on",
                table: "WorkItemDependencies",
                column: "DependsOnWorkItemId");

            migrationBuilder.CreateIndex(
                name: "uq_work_item_dependencies_item_depends_type",
                table: "WorkItemDependencies",
                columns: new[] { "WorkItemId", "DependsOnWorkItemId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLabels_LabelId",
                table: "WorkItemLabels",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_created_at",
                table: "WorkItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_created_by",
                table: "WorkItems",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_due_date",
                table: "WorkItems",
                column: "DueDate",
                filter: "\"DueDate\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_is_archived",
                table: "WorkItems",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_is_deleted",
                table: "WorkItems",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_parent",
                table: "WorkItems",
                column: "ParentWorkItemId");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_priority",
                table: "WorkItems",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "ix_work_items_product_type",
                table: "WorkItems",
                columns: new[] { "ProductId", "Type" });

            migrationBuilder.CreateIndex(
                name: "ix_work_items_swimlane_position",
                table: "WorkItems",
                columns: new[] { "SwimlaneId", "Position" });

            migrationBuilder.CreateIndex(
                name: "uq_work_items_product_number",
                table: "WorkItems",
                columns: new[] { "ProductId", "ItemNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activities");

            migrationBuilder.DropTable(
                name: "ChecklistItems");

            migrationBuilder.DropTable(
                name: "ItemTemplates");

            migrationBuilder.DropTable(
                name: "PokerVotes");

            migrationBuilder.DropTable(
                name: "ProductMembers");

            migrationBuilder.DropTable(
                name: "ProductTemplates");

            migrationBuilder.DropTable(
                name: "ReviewSessionParticipants");

            migrationBuilder.DropTable(
                name: "SprintItems");

            migrationBuilder.DropTable(
                name: "TeamRoles");

            migrationBuilder.DropTable(
                name: "TimeEntries");

            migrationBuilder.DropTable(
                name: "WorkItemAssignments");

            migrationBuilder.DropTable(
                name: "WorkItemAttachments");

            migrationBuilder.DropTable(
                name: "WorkItemComments");

            migrationBuilder.DropTable(
                name: "WorkItemDependencies");

            migrationBuilder.DropTable(
                name: "WorkItemLabels");

            migrationBuilder.DropTable(
                name: "Checklists");

            migrationBuilder.DropTable(
                name: "PokerSessions");

            migrationBuilder.DropTable(
                name: "Sprints");

            migrationBuilder.DropTable(
                name: "Labels");

            migrationBuilder.DropTable(
                name: "ReviewSessions");

            migrationBuilder.DropTable(
                name: "WorkItems");

            migrationBuilder.DropTable(
                name: "Swimlanes");

            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
