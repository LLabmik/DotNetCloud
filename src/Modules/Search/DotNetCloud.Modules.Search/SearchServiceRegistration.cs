using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.Data.Infrastructure;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Search.Events;
using DotNetCloud.Modules.Search.Extractors;
using DotNetCloud.Modules.Search.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using IContentExtractor = DotNetCloud.Core.Capabilities.IContentExtractor;
using ISearchProvider = DotNetCloud.Core.Capabilities.ISearchProvider;

namespace DotNetCloud.Modules.Search;

/// <summary>
/// Extension methods for registering Search module services in the DI container.
/// </summary>
public static class SearchServiceRegistration
{
    /// <summary>
    /// Registers all Search module service implementations in the DI container.
    /// ISearchableModule implementations must be registered separately by each module or in the server host.
    /// </summary>
    public static IServiceCollection AddSearchServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.TryAddSingleton<IBackgroundServiceTracker, BackgroundServiceTracker>();

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

        // Content extractors
        services.AddSingleton<IContentExtractor, PlainTextExtractor>();
        services.AddSingleton<IContentExtractor, MarkdownContentExtractor>();
        services.AddSingleton<IContentExtractor, HtmlContentExtractor>();
        services.AddSingleton<IContentExtractor, RtfContentExtractor>();
        services.AddSingleton<IContentExtractor, PdfContentExtractor>();
        services.AddSingleton<IContentExtractor, DocxContentExtractor>();
        services.AddSingleton<IContentExtractor, XlsxContentExtractor>();
        services.AddSingleton<IContentExtractor, PptxContentExtractor>();
        services.AddSingleton<IContentExtractor, OdfContentExtractor>();
        services.AddSingleton<IContentExtractor, XlsContentExtractor>();

        // Search services
        services.AddScoped<SearchQueryService>();
        services.AddScoped<ContentExtractionService>();
        services.AddSingleton<SearchIndexingService>();

        // Background reindex service (registered as singleton to allow controller injection)
        services.AddSingleton<SearchReindexBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<SearchReindexBackgroundService>());

        // Event handler for real-time indexing
        services.AddScoped<SearchIndexRequestEventHandler>();

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
