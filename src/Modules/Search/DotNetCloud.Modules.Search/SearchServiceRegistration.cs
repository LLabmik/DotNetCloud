using DotNetCloud.Core.Capabilities;
using DotNetCloud.Modules.Search.Events;
using DotNetCloud.Modules.Search.Extractors;
using DotNetCloud.Modules.Search.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        // Search provider — PostgreSQL as default
        services.AddScoped<ISearchProvider, PostgreSqlSearchProvider>();

        // Content extractors
        services.AddSingleton<IContentExtractor, PlainTextExtractor>();
        services.AddSingleton<IContentExtractor, MarkdownContentExtractor>();
        services.AddSingleton<IContentExtractor, PdfContentExtractor>();
        services.AddSingleton<IContentExtractor, DocxContentExtractor>();
        services.AddSingleton<IContentExtractor, XlsxContentExtractor>();

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
}
