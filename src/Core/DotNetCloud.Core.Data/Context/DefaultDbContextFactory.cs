using Microsoft.EntityFrameworkCore;
using DotNetCloud.Core.Data.Naming;

namespace DotNetCloud.Core.Data.Context;

/// <summary>
/// Default implementation of IDbContextFactory that creates CoreDbContext instances
/// configured for the appropriate database provider with proper naming strategies.
/// </summary>
public class DefaultDbContextFactory : IDbContextFactory
{
    private readonly string _connectionString;
    private readonly DatabaseProvider _provider;
    private readonly ITableNamingStrategy _namingStrategy;

    /// <summary>
    /// Creates a new instance of DefaultDbContextFactory.
    /// </summary>
    /// <param name="connectionString">The database connection string</param>
    /// <exception cref="ArgumentException">If connection string is null or empty</exception>
    /// <exception cref="InvalidOperationException">If provider cannot be detected from connection string</exception>
    public DefaultDbContextFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _connectionString = connectionString;
        _provider = DatabaseProviderDetector.DetectProvider(connectionString);
        _namingStrategy = DatabaseProviderDetector.GetNamingStrategy(_provider);
    }

    /// <summary>
    /// Creates a new instance of DefaultDbContextFactory with an explicit provider specification.
    /// Useful for testing or when connection string provider detection is ambiguous.
    /// </summary>
    /// <param name="connectionString">The database connection string</param>
    /// <param name="provider">The explicitly specified database provider</param>
    /// <exception cref="ArgumentException">If connection string is null or empty</exception>
    public DefaultDbContextFactory(string connectionString, DatabaseProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        _connectionString = connectionString;
        _provider = provider;
        _namingStrategy = DatabaseProviderDetector.GetNamingStrategy(provider);
    }

    public DatabaseProvider Provider => _provider;

    public ITableNamingStrategy NamingStrategy => _namingStrategy;

    public CoreDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>();

        // Configure the appropriate database provider
        ConfigureDbContextOptions(options);

        return new CoreDbContext(options.Options, _namingStrategy);
    }

    private void ConfigureDbContextOptions(DbContextOptionsBuilder<CoreDbContext> options)
    {
        switch (_provider)
        {
            case DatabaseProvider.PostgreSQL:
                options.UseNpgsql(_connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    npgsqlOptions.CommandTimeout(30);
                });
                break;

            case DatabaseProvider.SqlServer:
                options.UseSqlServer(_connectionString, sqlServerOptions =>
                {
                    sqlServerOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    sqlServerOptions.CommandTimeout(30);
                    sqlServerOptions.MigrationsAssembly("DotNetCloud.Core.Data.SqlServer");
                });
                break;

            case DatabaseProvider.MariaDB:
                // TODO: Add MariaDB support when Pomelo.EntityFrameworkCore.MySql .NET 10 compatible version is released
                throw new NotSupportedException("MariaDB support will be added when Pomelo.EntityFrameworkCore.MySql package is updated for .NET 10");

            default:
                throw new InvalidOperationException($"Unsupported database provider: {_provider}");
        }
    }
}
