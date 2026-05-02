using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Email.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Email.Data;

/// <summary>
/// Entity Framework Core database context for the Email module.
/// </summary>
public class EmailDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>Email accounts.</summary>
    public DbSet<EmailAccount> EmailAccounts => Set<EmailAccount>();

    /// <summary>Email mailboxes/labels.</summary>
    public DbSet<EmailMailbox> EmailMailboxes => Set<EmailMailbox>();

    /// <summary>Email threads.</summary>
    public DbSet<EmailThread> EmailThreads => Set<EmailThread>();

    /// <summary>Email messages.</summary>
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

    /// <summary>Email attachments.</summary>
    public DbSet<EmailAttachment> EmailAttachments => Set<EmailAttachment>();

    /// <summary>Email rules.</summary>
    public DbSet<EmailRule> EmailRules => Set<EmailRule>();

    /// <summary>Rule condition groups.</summary>
    public DbSet<EmailRuleConditionGroup> EmailRuleConditionGroups => Set<EmailRuleConditionGroup>();

    /// <summary>Rule conditions.</summary>
    public DbSet<EmailRuleCondition> EmailRuleConditions => Set<EmailRuleCondition>();

    /// <summary>Rule actions.</summary>
    public DbSet<EmailRuleAction> EmailRuleActions => Set<EmailRuleAction>();

    /// <summary>
    /// Initializes a new instance with the default PostgreSQL naming strategy.
    /// </summary>
    public EmailDbContext(DbContextOptions<EmailDbContext> options)
        : this(options, new PostgreSqlNamingStrategy()) { }

    /// <summary>
    /// Initializes a new instance with the specified naming strategy.
    /// </summary>
    public EmailDbContext(DbContextOptions<EmailDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("email"));
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new Configuration.EmailAccountConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.EmailMailboxConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.EmailThreadConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.EmailMessageConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.EmailAttachmentConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.EmailRuleConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.EmailRuleConditionGroupConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.EmailRuleConditionConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.EmailRuleActionConfiguration());
    }
}
