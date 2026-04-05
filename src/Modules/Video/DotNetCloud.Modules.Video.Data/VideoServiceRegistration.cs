using DotNetCloud.Modules.Video.Data.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Video.Data;

/// <summary>
/// Registers Video module services for dependency injection.
/// </summary>
public static class VideoServiceRegistration
{
    /// <summary>
    /// Adds Video module services to the DI container.
    /// </summary>
    public static IServiceCollection AddVideoServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<VideoService>();
        services.AddScoped<VideoCollectionService>();
        services.AddScoped<SubtitleService>();
        services.AddScoped<WatchProgressService>();
        services.AddScoped<VideoMetadataService>();
        services.AddScoped<VideoStreamingService>();

        return services;
    }
}
