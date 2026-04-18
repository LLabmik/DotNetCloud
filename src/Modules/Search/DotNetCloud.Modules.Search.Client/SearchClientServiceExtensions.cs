using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Search.Client;

/// <summary>
/// Extension methods for registering the Search FTS gRPC client in dependency injection.
/// </summary>
public static class SearchClientServiceExtensions
{
    /// <summary>
    /// Adds the Search FTS gRPC client to the service collection.
    /// Reads configuration from the "SearchModule" section.
    /// </summary>
    public static IServiceCollection AddSearchFtsClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SearchFtsClientOptions>(configuration.GetSection(SearchFtsClientOptions.SectionName));
        services.AddSingleton<ISearchFtsClient, SearchFtsClient>();
        return services;
    }

    /// <summary>
    /// Adds the Search FTS gRPC client with a specific address.
    /// </summary>
    public static IServiceCollection AddSearchFtsClient(this IServiceCollection services, string searchModuleAddress)
    {
        services.Configure<SearchFtsClientOptions>(options =>
        {
            options.SearchModuleAddress = searchModuleAddress;
        });
        services.AddSingleton<ISearchFtsClient, SearchFtsClient>();
        return services;
    }
}
