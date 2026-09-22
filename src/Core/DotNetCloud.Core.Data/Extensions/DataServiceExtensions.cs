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
        services.AddCoreDbContextFactory(connectionString, provider);

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

    /// <summary>
    /// Registers the factory that hands out short-lived <see cref="CoreDbContext"/> instances.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="connectionString">The database connection string</param>
    /// <param name="provider">The configured database provider</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    /// Any host that registers a <see cref="CoreDbContext"/> must also register this factory whenever it
    /// activates a Core service that owns its own context per operation (for example
    /// <c>UserSettingsService</c>, <c>AdminSettingsService</c> or the directory services in
    /// <c>DotNetCloud.Core.Auth</c>). Those services deliberately do not capture a context, so without the
    /// factory the host fails at runtime with "Unable to resolve service for type 'IDbContextFactory'" -
    /// a whole module can go unhealthy for it. Process-isolated module hosts build their own container, so
    /// this is not inherited from Core.Server: call it next to every raw AddDbContext&lt;CoreDbContext&gt;.
    /// </remarks>
    public static IServiceCollection AddCoreDbContextFactory(
        this IServiceCollection services,
        string connectionString,
        DatabaseProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IDbContextFactory>(_ => new DefaultDbContextFactory(connectionString, provider));
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
