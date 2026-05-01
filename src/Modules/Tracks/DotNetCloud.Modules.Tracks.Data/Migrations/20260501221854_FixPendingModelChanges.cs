using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetCloud.Modules.Tracks.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "WorkItemShareLinks",
                newName: "WorkItemShareLinks",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "WorkItems",
                newName: "WorkItems",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "WorkItemLabels",
                newName: "WorkItemLabels",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "WorkItemFieldValues",
                newName: "WorkItemFieldValues",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "WorkItemDependencies",
                newName: "WorkItemDependencies",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "WorkItemComments",
                newName: "WorkItemComments",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "WorkItemAttachments",
                newName: "WorkItemAttachments",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "WorkItemAssignments",
                newName: "WorkItemAssignments",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "WebhookSubscriptions",
                newName: "WebhookSubscriptions",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "WebhookDeliveries",
                newName: "WebhookDeliveries",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "TracksTeams",
                newName: "TracksTeams",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "TimeEntries",
                newName: "TimeEntries",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "TeamRoles",
                newName: "TeamRoles",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "SwimlaneTransitionRules",
                newName: "SwimlaneTransitionRules",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "Swimlanes",
                newName: "Swimlanes",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "Sprints",
                newName: "Sprints",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "SprintItems",
                newName: "SprintItems",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "ReviewSessions",
                newName: "ReviewSessions",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "ReviewSessionParticipants",
                newName: "ReviewSessionParticipants",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "RecurringRules",
                newName: "RecurringRules",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "ProductTemplates",
                newName: "ProductTemplates",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "Products",
                newName: "Products",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "ProductMembers",
                newName: "ProductMembers",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "PokerVotes",
                newName: "PokerVotes",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "PokerSessions",
                newName: "PokerSessions",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "Milestones",
                newName: "Milestones",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "Labels",
                newName: "Labels",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "ItemTemplates",
                newName: "ItemTemplates",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "GuestUsers",
                newName: "GuestUsers",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "GuestPermissions",
                newName: "GuestPermissions",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "GoalWorkItems",
                newName: "GoalWorkItems",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "Goals",
                newName: "Goals",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "CustomFields",
                newName: "CustomFields",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "CommentReactions",
                newName: "CommentReactions",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "Checklists",
                newName: "Checklists",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "ChecklistItems",
                newName: "ChecklistItems",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "AutomationRules",
                newName: "AutomationRules",
                newSchema: "tracks");

            migrationBuilder.RenameTable(
                name: "Activities",
                newName: "Activities",
                newSchema: "tracks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "WorkItemShareLinks",
                schema: "tracks",
                newName: "WorkItemShareLinks");

            migrationBuilder.RenameTable(
                name: "WorkItems",
                schema: "tracks",
                newName: "WorkItems");

            migrationBuilder.RenameTable(
                name: "WorkItemLabels",
                schema: "tracks",
                newName: "WorkItemLabels");

            migrationBuilder.RenameTable(
                name: "WorkItemFieldValues",
                schema: "tracks",
                newName: "WorkItemFieldValues");

            migrationBuilder.RenameTable(
                name: "WorkItemDependencies",
                schema: "tracks",
                newName: "WorkItemDependencies");

            migrationBuilder.RenameTable(
                name: "WorkItemComments",
                schema: "tracks",
                newName: "WorkItemComments");

            migrationBuilder.RenameTable(
                name: "WorkItemAttachments",
                schema: "tracks",
                newName: "WorkItemAttachments");

            migrationBuilder.RenameTable(
                name: "WorkItemAssignments",
                schema: "tracks",
                newName: "WorkItemAssignments");

            migrationBuilder.RenameTable(
                name: "WebhookSubscriptions",
                schema: "tracks",
                newName: "WebhookSubscriptions");

            migrationBuilder.RenameTable(
                name: "WebhookDeliveries",
                schema: "tracks",
                newName: "WebhookDeliveries");

            migrationBuilder.RenameTable(
                name: "TracksTeams",
                schema: "tracks",
                newName: "TracksTeams");

            migrationBuilder.RenameTable(
                name: "TimeEntries",
                schema: "tracks",
                newName: "TimeEntries");

            migrationBuilder.RenameTable(
                name: "TeamRoles",
                schema: "tracks",
                newName: "TeamRoles");

            migrationBuilder.RenameTable(
                name: "SwimlaneTransitionRules",
                schema: "tracks",
                newName: "SwimlaneTransitionRules");

            migrationBuilder.RenameTable(
                name: "Swimlanes",
                schema: "tracks",
                newName: "Swimlanes");

            migrationBuilder.RenameTable(
                name: "Sprints",
                schema: "tracks",
                newName: "Sprints");

            migrationBuilder.RenameTable(
                name: "SprintItems",
                schema: "tracks",
                newName: "SprintItems");

            migrationBuilder.RenameTable(
                name: "ReviewSessions",
                schema: "tracks",
                newName: "ReviewSessions");

            migrationBuilder.RenameTable(
                name: "ReviewSessionParticipants",
                schema: "tracks",
                newName: "ReviewSessionParticipants");

            migrationBuilder.RenameTable(
                name: "RecurringRules",
                schema: "tracks",
                newName: "RecurringRules");

            migrationBuilder.RenameTable(
                name: "ProductTemplates",
                schema: "tracks",
                newName: "ProductTemplates");

            migrationBuilder.RenameTable(
                name: "Products",
                schema: "tracks",
                newName: "Products");

            migrationBuilder.RenameTable(
                name: "ProductMembers",
                schema: "tracks",
                newName: "ProductMembers");

            migrationBuilder.RenameTable(
                name: "PokerVotes",
                schema: "tracks",
                newName: "PokerVotes");

            migrationBuilder.RenameTable(
                name: "PokerSessions",
                schema: "tracks",
                newName: "PokerSessions");

            migrationBuilder.RenameTable(
                name: "Milestones",
                schema: "tracks",
                newName: "Milestones");

            migrationBuilder.RenameTable(
                name: "Labels",
                schema: "tracks",
                newName: "Labels");

            migrationBuilder.RenameTable(
                name: "ItemTemplates",
                schema: "tracks",
                newName: "ItemTemplates");

            migrationBuilder.RenameTable(
                name: "GuestUsers",
                schema: "tracks",
                newName: "GuestUsers");

            migrationBuilder.RenameTable(
                name: "GuestPermissions",
                schema: "tracks",
                newName: "GuestPermissions");

            migrationBuilder.RenameTable(
                name: "GoalWorkItems",
                schema: "tracks",
                newName: "GoalWorkItems");

            migrationBuilder.RenameTable(
                name: "Goals",
                schema: "tracks",
                newName: "Goals");

            migrationBuilder.RenameTable(
                name: "CustomFields",
                schema: "tracks",
                newName: "CustomFields");

            migrationBuilder.RenameTable(
                name: "CommentReactions",
                schema: "tracks",
                newName: "CommentReactions");

            migrationBuilder.RenameTable(
                name: "Checklists",
                schema: "tracks",
                newName: "Checklists");

            migrationBuilder.RenameTable(
                name: "ChecklistItems",
                schema: "tracks",
                newName: "ChecklistItems");

            migrationBuilder.RenameTable(
                name: "AutomationRules",
                schema: "tracks",
                newName: "AutomationRules");

            migrationBuilder.RenameTable(
                name: "Activities",
                schema: "tracks",
                newName: "Activities");
        }
    }
}
