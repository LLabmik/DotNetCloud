using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Events;
using DotNetCloud.Modules.Video.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        // Business services (forward-registered for concrete + interface injection)
        services.AddScoped<VideoService>();
        services.AddScoped<IVideoService>(sp => sp.GetRequiredService<VideoService>());
        services.AddScoped<VideoCollectionService>();
        services.AddScoped<IVideoCollectionService>(sp => sp.GetRequiredService<VideoCollectionService>());
        services.AddScoped<SubtitleService>();
        services.AddScoped<ISubtitleService>(sp => sp.GetRequiredService<SubtitleService>());
        services.AddScoped<WatchProgressService>();
        services.AddScoped<IWatchProgressService>(sp => sp.GetRequiredService<WatchProgressService>());
        services.AddScoped<VideoMetadataService>();
        services.AddScoped<IVideoMetadataService>(sp => sp.GetRequiredService<VideoMetadataService>());
        services.AddScoped<VideoStreamingService>();
        services.AddScoped<IVideoStreamingService>(sp => sp.GetRequiredService<VideoStreamingService>());

        // Thumbnail service (FFmpeg + ImageSharp)
        services.AddScoped<VideoThumbnailService>();
        services.AddScoped<IVideoThumbnailService>(sp => sp.GetRequiredService<VideoThumbnailService>());

        // TMDB API client
        var tmdbRateLimitMs = configuration.GetValue("Video:Enrichment:TmdbRateLimitMs", 300);
        services.AddSingleton(sp => new TmdbRateLimiter(tmdbRateLimitMs, sp.GetRequiredService<ILogger<TmdbRateLimiter>>()));
        services.AddHttpClient<ITmdbClient, TmdbClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "DotNetCloud/0.1");
        });

        // Video enrichment services
        services.AddScoped<VideoEnrichmentService>();
        services.AddScoped<IVideoEnrichmentService>(sp => sp.GetRequiredService<VideoEnrichmentService>());

        // Background enrichment queue (singleton — shared across the application lifetime)
        services.AddSingleton<InMemoryVideoEnrichmentBackgroundQueue>();
        services.AddSingleton<IVideoEnrichmentBackgroundQueue>(sp => sp.GetRequiredService<InMemoryVideoEnrichmentBackgroundQueue>());
        services.AddHostedService<VideoEnrichmentBackgroundService>();

        // Indexing callback (bridges Module → Data for FileUploadedEvent handling)
        services.AddScoped<IVideoIndexingCallback, VideoIndexingCallback>();

        // Event handlers
        services.AddScoped<IEventHandler<FileUploadedEvent>, FileUploadedVideoHandler>();
        services.AddScoped<IEventHandler<ResourceSharedEvent>, VideoSharedNotificationHandler>();

        return services;
    }
}
