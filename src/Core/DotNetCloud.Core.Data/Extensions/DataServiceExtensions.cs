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
    /// <param name="provider">The configured database provider</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddDotNetCloudDbContext(
        this IServiceCollection services,
        string connectionString,
        DatabaseProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Register the naming strategy for the provider
        var namingStrategy = DatabaseProviderDetector.GetNamingStrategy(provider);
        services.AddSingleton(namingStrategy);

        // Register DbContext factory
        services.AddSingleton<IDbContextFactory>(sp => new DefaultDbContextFactory(connectionString, provider));

        // Blazor Server uses Transient to prevent concurrent component render
        // errors ("second operation started on this context instance").
        services.AddDbContext<CoreDbContext>((sp, options) =>
        {
            ConfigureDbContext(options, provider, connectionString);
        }, ServiceLifetime.Transient);

        // Register DbInitializer
        services.AddScoped<DbInitializer>();

        // Register schema services
        services.AddSingleton<IModuleSchemaProvider, SelfManagedSchemaProvider>();
        services.AddSingleton<ModuleSchemaService>();

        return services;
    }

    private static void ConfigureDbContext(DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString)
    {
        DbResiliencePolicy.Configure(
            options,
            provider,
            connectionString,
            provider == DatabaseProvider.SqlServer ? "DotNetCloud.Core.Data.SqlServer" : null);

        // Common options
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        options.EnableDetailedErrors();
    }
}
