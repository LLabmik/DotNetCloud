using DotNetCloud.Core.Data.Context;
using DotNetCloud.Core.Data.Initialization;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Data.Services;
using DotNetCloud.Core.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.Data.Extensions;

/// <summary>
/// Extension methods for registering the DotNetCloud data layer services.
/// </summary>
public static class DataServiceExtensions
{
    /// <summary>
    /// Add the DotNetCloud database context and data services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">The database connection string</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotNetCloudDbContext(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Detect database provider from connection string
        var provider = DatabaseProviderDetector.DetectProvider(connectionString);

        // Register the naming strategy for the provider
        var namingStrategy = DatabaseProviderDetector.GetNamingStrategy(provider);
        services.AddSingleton(namingStrategy);

        // Register DbContext factory
        services.AddSingleton<IDbContextFactory>(sp => new DefaultDbContextFactory(connectionString, provider));

        // Register DbContext
        services.AddDbContext<CoreDbContext>((sp, options) =>
        {
            ConfigureDbContext(options, provider, connectionString);
        });

        // Register DbInitializer
        services.AddScoped<DbInitializer>();

        // Register schema services
        services.AddSingleton<IModuleSchemaProvider, SelfManagedSchemaProvider>();
        services.AddSingleton<ModuleSchemaService>();

        return services;
    }

    private static void ConfigureDbContext(DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString)
    {
        switch (provider)
        {
            case DatabaseProvider.PostgreSQL:
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    npgsqlOptions.CommandTimeout(30);
                });
                break;

            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString, sqlServerOptions =>
                {
                    sqlServerOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    sqlServerOptions.CommandTimeout(30);
                    sqlServerOptions.MigrationsAssembly("DotNetCloud.Core.Data.SqlServer");
                });
                break;

            case DatabaseProvider.MariaDB:
                // MariaDB support deferred until Pomelo updates to .NET 10
                throw new NotSupportedException("MariaDB support is temporarily disabled pending Pomelo .NET 10 update");

            default:
                throw new InvalidOperationException($"Unsupported database provider: {provider}");
        }

        // Common options
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        options.EnableDetailedErrors();
    }
}
