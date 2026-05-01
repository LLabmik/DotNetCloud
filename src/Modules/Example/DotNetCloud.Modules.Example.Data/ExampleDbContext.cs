using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Modules.Example.Data.Configuration;
using DotNetCloud.Modules.Example.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Example.Data;

/// <summary>
/// Database context for the Example module.
/// Demonstrates how modules create their own DbContext for data persistence.
/// </summary>
/// <remarks>
/// The Example module uses a self-managed database schema. The schema name is
/// determined by <see cref="ITableNamingStrategy.GetSchemaForModule"/> at startup.
/// Third-party modules should follow this same pattern: inject the naming strategy,
/// call <c>HasDefaultSchema</c> in <c>OnModelCreating</c>, and self-migrate on startup.
/// </remarks>
public class ExampleDbContext : DbContext
{
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExampleDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by the DbContext.</param>
    /// <param name="namingStrategy">The table naming strategy for schema and table name conventions.</param>
    public ExampleDbContext(DbContextOptions<ExampleDbContext> options, ITableNamingStrategy namingStrategy)
        : base(options)
    {
        _namingStrategy = namingStrategy;
    }

    /// <summary>
    /// Gets the example notes table.
    /// </summary>
    public DbSet<ExampleNote> Notes => Set<ExampleNote>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(_namingStrategy.GetSchemaForModule("example"));
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new ExampleNoteConfiguration());
    }
}
