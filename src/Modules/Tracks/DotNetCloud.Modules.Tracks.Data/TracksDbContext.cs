using DotNetCloud.Modules.Tracks.Data.Configuration;
using DotNetCloud.Modules.Tracks.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Tracks.Data;

/// <summary>
/// Entity Framework Core DbContext for the Tracks module.
/// Manages all project management entities: boards, lists, cards, labels, sprints, and related data.
/// </summary>
/// <remarks>
/// <para>
/// <b>Module DbContext Pattern:</b>
/// Each module owns its own DbContext, separate from the core <c>CoreDbContext</c>.
/// This provides schema isolation, independent migrations, and testability.
/// </para>
/// <para>
/// <b>Multi-Database Support:</b>
/// Works with PostgreSQL, SQL Server, and MariaDB through provider-specific configuration.
/// </para>
/// </remarks>
public class TracksDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TracksDbContext"/> class.
    /// </summary>
    public TracksDbContext(DbContextOptions<TracksDbContext> options) : base(options) { }

    /// <summary>Project boards.</summary>
    public DbSet<Board> Boards => Set<Board>();

    /// <summary>Board membership records.</summary>
    public DbSet<BoardMember> BoardMembers => Set<BoardMember>();

    /// <summary>Board lists (columns).</summary>
    public DbSet<BoardList> BoardLists => Set<BoardList>();

    /// <summary>Cards (work items).</summary>
    public DbSet<Card> Cards => Set<Card>();

    /// <summary>Card user assignments.</summary>
    public DbSet<CardAssignment> CardAssignments => Set<CardAssignment>();

    /// <summary>Board labels.</summary>
    public DbSet<Label> Labels => Set<Label>();

    /// <summary>Card-label join records.</summary>
    public DbSet<CardLabel> CardLabels => Set<CardLabel>();

    /// <summary>Card comments.</summary>
    public DbSet<CardComment> CardComments => Set<CardComment>();

    /// <summary>Card file attachments.</summary>
    public DbSet<CardAttachment> CardAttachments => Set<CardAttachment>();

    /// <summary>Card checklists.</summary>
    public DbSet<CardChecklist> CardChecklists => Set<CardChecklist>();

    /// <summary>Checklist items.</summary>
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();

    /// <summary>Card dependency relationships.</summary>
    public DbSet<CardDependency> CardDependencies => Set<CardDependency>();

    /// <summary>Sprints.</summary>
    public DbSet<Sprint> Sprints => Set<Sprint>();

    /// <summary>Sprint-card join records.</summary>
    public DbSet<SprintCard> SprintCards => Set<SprintCard>();

    /// <summary>Time tracking entries.</summary>
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();

    /// <summary>Board activity audit log.</summary>
    public DbSet<BoardActivity> BoardActivities => Set<BoardActivity>();

    /// <summary>Planning poker sessions.</summary>
    public DbSet<PokerSession> PokerSessions => Set<PokerSession>();

    /// <summary>Planning poker votes.</summary>
    public DbSet<PokerVote> PokerVotes => Set<PokerVote>();

    /// <summary>Tracks-specific team role assignments (maps Core team members to Tracks roles).</summary>
    public DbSet<TeamRole> TeamRoles => Set<TeamRole>();

    /// <summary>Board templates for pre-configured board creation.</summary>
    public DbSet<BoardTemplate> BoardTemplates => Set<BoardTemplate>();

    /// <summary>Card templates for pre-configured card creation.</summary>
    public DbSet<CardTemplate> CardTemplates => Set<CardTemplate>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new BoardConfiguration());
        modelBuilder.ApplyConfiguration(new BoardMemberConfiguration());
        modelBuilder.ApplyConfiguration(new BoardListConfiguration());
        modelBuilder.ApplyConfiguration(new CardConfiguration());
        modelBuilder.ApplyConfiguration(new CardAssignmentConfiguration());
        modelBuilder.ApplyConfiguration(new LabelConfiguration());
        modelBuilder.ApplyConfiguration(new CardLabelConfiguration());
        modelBuilder.ApplyConfiguration(new CardCommentConfiguration());
        modelBuilder.ApplyConfiguration(new CardAttachmentConfiguration());
        modelBuilder.ApplyConfiguration(new CardChecklistConfiguration());
        modelBuilder.ApplyConfiguration(new ChecklistItemConfiguration());
        modelBuilder.ApplyConfiguration(new CardDependencyConfiguration());
        modelBuilder.ApplyConfiguration(new SprintConfiguration());
        modelBuilder.ApplyConfiguration(new SprintCardConfiguration());
        modelBuilder.ApplyConfiguration(new TimeEntryConfiguration());
        modelBuilder.ApplyConfiguration(new BoardActivityConfiguration());
        modelBuilder.ApplyConfiguration(new PokerSessionConfiguration());
        modelBuilder.ApplyConfiguration(new PokerVoteConfiguration());
        modelBuilder.ApplyConfiguration(new TeamRoleConfiguration());
        modelBuilder.ApplyConfiguration(new BoardTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new CardTemplateConfiguration());
    }
}
