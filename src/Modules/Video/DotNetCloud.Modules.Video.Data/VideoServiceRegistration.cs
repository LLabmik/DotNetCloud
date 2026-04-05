using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Events;
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
        // Business services
        services.AddScoped<VideoService>();
        services.AddScoped<VideoCollectionService>();
        services.AddScoped<SubtitleService>();
        services.AddScoped<WatchProgressService>();
        services.AddScoped<VideoMetadataService>();
        services.AddScoped<VideoStreamingService>();

        // Indexing callback (bridges Module → Data for FileUploadedEvent handling)
        services.AddScoped<IVideoIndexingCallback, VideoIndexingCallback>();

        // Event handlers
        services.AddScoped<IEventHandler<FileUploadedEvent>, FileUploadedVideoHandler>();
        services.AddScoped<IEventHandler<ResourceSharedEvent>, VideoSharedNotificationHandler>();

        return services;
    }
}
