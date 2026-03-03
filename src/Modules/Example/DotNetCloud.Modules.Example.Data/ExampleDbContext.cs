using DotNetCloud.Modules.Example.Data.Configuration;
using DotNetCloud.Modules.Example.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Modules.Example.Data;

/// <summary>
/// Database context for the Example module.
/// Demonstrates how modules create their own DbContext for data persistence.
/// </summary>
/// <remarks>
/// <para>
/// <b>Module DbContext Pattern:</b>
/// Each module owns its own DbContext, separate from the core <c>CoreDbContext</c>.
/// This provides:
/// <list type="bullet">
///   <item><description>Isolation: module schema changes don't affect other modules</description></item>
///   <item><description>Independence: modules manage their own migrations</description></item>
///   <item><description>Testability: modules can be tested with in-memory databases</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Multi-Database Support:</b>
/// The context works with PostgreSQL, SQL Server, and MariaDB through
/// provider-specific configuration passed via <see cref="DbContextOptions"/>.
/// </para>
/// </remarks>
public class ExampleDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExampleDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to be used by the DbContext.</param>
    public ExampleDbContext(DbContextOptions<ExampleDbContext> options)
        : base(options)
    {
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
    }
}
