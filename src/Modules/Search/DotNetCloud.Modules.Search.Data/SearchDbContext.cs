using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Search.Data.Configuration;
using DotNetCloud.Modules.Search.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Search.Data;

/// <summary>
/// Database context for the Search module.
/// Manages the search index entries and indexing job tracking.
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
/// Provider-specific full-text search indexes are applied via migrations.
/// </para>
/// </remarks>
public class SearchDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchDbContext"/> class.
    /// </summary>
    public SearchDbContext(DbContextOptions<SearchDbContext> options)
        : this(options, new PostgreSqlNamingStrategy())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchDbContext"/> class with a specific naming strategy.
    /// </summary>
    public SearchDbContext(DbContextOptions<SearchDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>The centralized search index entries.</summary>
    public DbSet<SearchIndexEntry> SearchIndexEntries => Set<SearchIndexEntry>();

    /// <summary>Reindex job tracking records.</summary>
    public DbSet<IndexingJob> IndexingJobs => Set<IndexingJob>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("search"));
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new SearchIndexEntryConfiguration());
        modelBuilder.ApplyConfiguration(new IndexingJobConfiguration());
    }
}
