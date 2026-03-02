using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Core.Data.Infrastructure;

/// <summary>
/// Default implementation of <see cref="IDbContextFactory{TContext}"/> that creates DbContext instances
/// with the appropriate database provider configuration.
/// </summary>
/// <typeparam name="TContext">The type of DbContext to create.</typeparam>
public class DefaultDbContextFactory<TContext> : IDbContextFactory<TContext> where TContext : DbContext
{
    private readonly DbContextOptions<TContext> _options;
    private readonly ILoggerFactory? _loggerFactory;

    /// <inheritdoc />
    public DatabaseProvider Provider { get; }

    /// <inheritdoc />
    public ITableNamingStrategy NamingStrategy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDbContextFactory{TContext}"/> class.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="loggerFactory">Optional logger factory for EF Core logging.</param>
    public DefaultDbContextFactory(string connectionString, ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _loggerFactory = loggerFactory;
        Provider = DatabaseProviderDetector.DetectProvider(connectionString);
        NamingStrategy = DatabaseProviderDetector.GetNamingStrategy(Provider);

        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        ConfigureProvider(optionsBuilder, connectionString);

        if (loggerFactory != null)
        {
            optionsBuilder.UseLoggerFactory(loggerFactory);
        }

        _options = optionsBuilder.Options;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultDbContextFactory{TContext}"/> class
    /// with explicit provider specification.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="provider">The database provider to use.</param>
    /// <param name="loggerFactory">Optional logger factory for EF Core logging.</param>
    public DefaultDbContextFactory(
        string connectionString,
        DatabaseProvider provider,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _loggerFactory = loggerFactory;
        Provider = provider;
        NamingStrategy = DatabaseProviderDetector.GetNamingStrategy(Provider);

        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        ConfigureProvider(optionsBuilder, connectionString);

        if (loggerFactory != null)
        {
            optionsBuilder.UseLoggerFactory(loggerFactory);
        }

        _options = optionsBuilder.Options;
    }

    /// <inheritdoc />
    public TContext CreateDbContext()
    {
        var context = (TContext)Activator.CreateInstance(typeof(TContext), _options)!;
        return context;
    }

    /// <summary>
    /// Configures the DbContext options builder with the appropriate database provider.
    /// </summary>
    /// <param name="optionsBuilder">The options builder to configure.</param>
    /// <param name="connectionString">The connection string.</param>
    private void ConfigureProvider(DbContextOptionsBuilder<TContext> optionsBuilder, string connectionString)
    {
        switch (Provider)
        {
            case DatabaseProvider.PostgreSQL:
                optionsBuilder.UseNpgsql(connectionString, options =>
                {
                    options.MigrationsHistoryTable("__ef_migrations_history", "core");
                    options.EnableRetryOnFailure(maxRetryCount: 3);
                });
                break;

            case DatabaseProvider.SqlServer:
                optionsBuilder.UseSqlServer(connectionString, options =>
                {
                    options.MigrationsHistoryTable("__EFMigrationsHistory", "core");
                    options.EnableRetryOnFailure(maxRetryCount: 3);
                });
                break;

            case DatabaseProvider.MariaDB:
                // TODO: Add Pomelo.EntityFrameworkCore.MySql when .NET 10 compatible version is available
                throw new NotSupportedException(
                    "MariaDB support is temporarily disabled until Pomelo.EntityFrameworkCore.MySql " +
                    "releases a .NET 10 compatible version. Use PostgreSQL or SQL Server instead.");

            default:
                throw new ArgumentOutOfRangeException(nameof(Provider), Provider, "Unsupported database provider.");
        }
    }
}
