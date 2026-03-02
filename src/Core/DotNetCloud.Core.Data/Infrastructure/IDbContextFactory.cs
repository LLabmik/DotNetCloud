using Microsoft.EntityFrameworkCore;

namespace DotNetCloud.Core.Data.Infrastructure;

/// <summary>
/// Factory for creating instances of <typeparamref name="TContext"/>.
/// </summary>
/// <typeparam name="TContext">The type of DbContext to create.</typeparam>
/// <remarks>
/// This abstraction allows for easy testing and dependency injection of DbContext instances.
/// It extends the built-in EF Core IDbContextFactory interface with provider-specific information.
/// </remarks>
public interface IDbContextFactory<TContext> : Microsoft.EntityFrameworkCore.IDbContextFactory<TContext> where TContext : DbContext
{
    /// <summary>
    /// Gets the database provider for this factory.
    /// </summary>
    DatabaseProvider Provider { get; }

    /// <summary>
    /// Gets the naming strategy for this factory.
    /// </summary>
    ITableNamingStrategy NamingStrategy { get; }
}
