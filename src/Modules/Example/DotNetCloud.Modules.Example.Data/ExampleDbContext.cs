using DotNetCloud.Core.Data.Configuration.Extensions;
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
/// The Example module uses a self-managed database schema, and that schema is <b>not</b> part of
/// the model: sandboxed modules receive a generated schema, pinned as the session
/// <c>search_path</c> by the connection string the core provisions. Keeping the model and its
/// migrations schema-neutral is what lets one migration apply to every store's schema (and keeps
/// EF from reporting pending model changes against the snapshot).
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
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new ExampleNoteConfiguration());

        SequentialGuidConfigurationExtensions.ApplySequentialGuidDefaults(modelBuilder, _namingStrategy.Provider);
    }
}
