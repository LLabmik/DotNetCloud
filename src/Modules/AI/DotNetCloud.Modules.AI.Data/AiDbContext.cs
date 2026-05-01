using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.AI.Data.Configuration;
using DotNetCloud.Modules.AI.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.AI.Data;

/// <summary>
/// EF Core DbContext for the AI module.
/// Manages conversation history and related entities.
/// </summary>
public class AiDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiDbContext"/> class.
    /// </summary>
    public AiDbContext(DbContextOptions<AiDbContext> options)
        : this(options, new PostgreSqlNamingStrategy())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AiDbContext"/> class with a specific naming strategy.
    /// </summary>
    public AiDbContext(DbContextOptions<AiDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>Gets or sets the conversations.</summary>
    public DbSet<Conversation> Conversations => Set<Conversation>();

    /// <summary>Gets or sets the conversation messages.</summary>
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("ai"));
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new ConversationConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationMessageConfiguration());
    }
}
