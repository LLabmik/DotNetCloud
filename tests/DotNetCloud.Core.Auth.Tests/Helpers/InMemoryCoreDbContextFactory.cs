using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Naming;
using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Auth.Tests.Helpers;

/// <summary>
/// Hands out one in-memory <see cref="CoreDbContext"/> per call and records every context it created,
/// so a test can prove that a service never shares a context between operations.
/// </summary>
/// <remarks>
/// These tests pin a contract rather than a race: the in-memory provider cannot force real query
/// overlap, so a concurrent test would pass even against a service that captures a single context.
/// "One context per operation, all released" is the property that actually removes the Blazor
/// circuit hazard ("a second operation was started on this context instance").
/// </remarks>
internal sealed class InMemoryCoreDbContextFactory : IDbContextFactory
{
    private readonly string _databaseName;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryCoreDbContextFactory"/> class.
    /// </summary>
    /// <param name="databaseName">The shared in-memory database name.</param>
    /// <param name="namingStrategy">The naming strategy for the created contexts.</param>
    public InMemoryCoreDbContextFactory(string databaseName, ITableNamingStrategy? namingStrategy = null)
    {
        _databaseName = databaseName;
        NamingStrategy = namingStrategy ?? new PostgreSqlNamingStrategy();
    }

    /// <summary>
    /// Gets every context handed out so far, in creation order.
    /// </summary>
    public List<CoreDbContext> CreatedContexts { get; } = [];

    /// <inheritdoc />
    public DatabaseProvider Provider => DatabaseProvider.PostgreSQL;

    /// <inheritdoc />
    public ITableNamingStrategy NamingStrategy { get; }

    /// <inheritdoc />
    public CoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;

        var context = new CoreDbContext(options, NamingStrategy);
        CreatedContexts.Add(context);
        return context;
    }
}
