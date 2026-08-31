using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Infrastructure;
using DotNetCloud.Core.Search.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ISearchProvider = DotNetCloud.Core.Capabilities.ISearchProvider;

namespace DotNetCloud.Core.Search;

/// <summary>
/// Extension methods for registering core-owned search services in the DI container.
/// Replaces the old Search module's <c>AddSearchServices</c>.
/// </summary>
public static class SearchServiceRegistration
{
    /// <summary>
    /// Registers the core search services (query service, parser, providers) in DI.
    /// The <see cref="ISearchProvider"/> implementation is auto-selected based on the
    /// configured database provider.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configuration">Optional configuration used to resolve the database provider.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddCoreSearchServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // SearchQueryService depends on the scoped ISearchProvider, so it must be scoped.
        services.TryAddScoped<SearchQueryService>();

        // Search provider — auto-selected based on database provider configuration
        var provider = ResolveDatabaseProvider(configuration);
        switch (provider)
        {
            case DatabaseProvider.SqlServer:
                services.AddScoped<ISearchProvider, SqlServerSearchProvider>();
                break;
            case DatabaseProvider.PostgreSQL:
            default:
                services.AddScoped<ISearchProvider, PostgreSqlSearchProvider>();
                break;
        }

        return services;
    }

    private static DatabaseProvider ResolveDatabaseProvider(IConfiguration? configuration)
    {
        if (configuration == null)
            return DatabaseProvider.PostgreSQL; // default

        var configuredProvider = configuration["Database:Provider"] ?? configuration["databaseProvider"];
        if (string.IsNullOrWhiteSpace(configuredProvider))
            return DatabaseProvider.PostgreSQL; // default

        // Normalize: "SqlServer" (from config.json) or "SQL Server" → DatabaseProvider.SqlServer
        var lower = configuredProvider.ToLowerInvariant();
        if (lower.Contains("sqlserver") || lower.Contains("sql server"))
            return DatabaseProvider.SqlServer;

        return DatabaseProvider.PostgreSQL;
    }
}
