using Microsoft.EntityFrameworkCore;
using DotNetCloud.Core.Data.Naming;

namespace DotNetCloud.Core.Data.Context;

/// <summary>
/// Factory for creating and configuring CoreDbContext instances with multi-database provider support.
/// Handles provider detection, naming strategy application, and context configuration.
/// </summary>
public interface IDbContextFactory
{
    /// <summary>
    /// Creates a new CoreDbContext instance configured for the current database provider.
    /// </summary>
    /// <returns>A configured CoreDbContext instance</returns>
    CoreDbContext CreateDbContext();

    /// <summary>
    /// Gets the current database provider.
    /// </summary>
    DatabaseProvider Provider { get; }

    /// <summary>
    /// Gets the naming strategy for the current database provider.
    /// </summary>
    ITableNamingStrategy NamingStrategy { get; }
}
