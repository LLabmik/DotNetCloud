using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data;

/// <summary>
/// Entity Framework Core DbContext for the Tracks module.
/// Manages all project management entities: products, work items, swimlanes, sprints, and related data.
/// </summary>
public class TracksDbContext : DbContext
{
    public TracksDbContext(DbContextOptions<TracksDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductMember> ProductMembers => Set<ProductMember>();
    public DbSet<Swimlane> Swimlanes => Set<Swimlane>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemAssignment> WorkItemAssignments => Set<WorkItemAssignment>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<WorkItemLabel> WorkItemLabels => Set<WorkItemLabel>();
    public DbSet<WorkItemComment> WorkItemComments => Set<WorkItemComment>();
    public DbSet<CommentReaction> CommentReactions => Set<CommentReaction>();
    public DbSet<WorkItemAttachment> WorkItemAttachments => Set<WorkItemAttachment>();
    public DbSet<Checklist> Checklists => Set<Checklist>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<WorkItemDependency> WorkItemDependencies => Set<WorkItemDependency>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<SprintItem> SprintItems => Set<SprintItem>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<PokerSession> PokerSessions => Set<PokerSession>();
    public DbSet<PokerVote> PokerVotes => Set<PokerVote>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamRole> TeamRoles => Set<TeamRole>();
    public DbSet<ProductTemplate> ProductTemplates => Set<ProductTemplate>();
    public DbSet<ItemTemplate> ItemTemplates => Set<ItemTemplate>();
    public DbSet<ReviewSession> ReviewSessions => Set<ReviewSession>();
    public DbSet<ReviewSessionParticipant> ReviewSessionParticipants => Set<ReviewSessionParticipant>();
    public DbSet<WorkItemWatcher> WorkItemWatchers => Set<WorkItemWatcher>();
    public DbSet<CustomView> CustomViews => Set<CustomView>();
    public DbSet<CustomField> CustomFields => Set<CustomField>();
    public DbSet<WorkItemFieldValue> WorkItemFieldValues => Set<WorkItemFieldValue>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<RecurringRule> RecurringRules => Set<RecurringRule>();
    public DbSet<WorkItemShareLink> WorkItemShareLinks => Set<WorkItemShareLink>();
    public DbSet<GuestUser> GuestUsers => Set<GuestUser>();
    public DbSet<GuestPermission> GuestPermissions => Set<GuestPermission>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<GoalWorkItem> GoalWorkItems => Set<GoalWorkItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TracksDbContext).Assembly);
    }
}
