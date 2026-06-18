using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Video.Data;
using DotNetCloud.Modules.Video.Data.Services;
using DotNetCloud.Modules.Video.Events;
using DotNetCloud.Modules.Video.Services;
using Microsoft.EntityFrameworkCore;
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
        // Register Files data services (provides IDownloadService needed by VideoThumbnailService)
        services.AddFilesServices(configuration);

        // Business services (forward-registered for concrete + interface injection)
        services.AddScoped<VideoService>();
        services.AddScoped<IVideoService>(sp => sp.GetRequiredService<VideoService>());
        services.AddScoped<VideoCollectionService>();
        services.AddScoped<IVideoCollectionService>(sp => sp.GetRequiredService<VideoCollectionService>());
        services.AddScoped<SubtitleService>();
        services.AddScoped<ISubtitleService>(sp => sp.GetRequiredService<SubtitleService>());
        services.AddScoped<VideoMetadataService>();
        services.AddScoped<IVideoMetadataService>(sp => sp.GetRequiredService<VideoMetadataService>());
        services.AddScoped<VideoStreamingService>();
        services.AddScoped<IVideoStreamingService>(sp => sp.GetRequiredService<VideoStreamingService>());
        services.AddScoped<VideoSeriesService>();
        services.AddScoped<IVideoSeriesService>(sp => sp.GetRequiredService<VideoSeriesService>());
        services.AddScoped<VideoSettingsProvider>();
        services.AddScoped<IVideoSettingsProvider>(sp => sp.GetRequiredService<VideoSettingsProvider>());

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

        // Scan progress state (singleton — shared across all video page sessions)
        services.AddSingleton<VideoScanProgressState>();

        // Stream preparation progress state (singleton — tracks chunk reconstruction / probing / remux)
        services.AddSingleton<StreamProgressState>();

        // Indexing callback (bridges Module → Data for FileUploadedEvent handling)
        services.AddScoped<IVideoIndexingCallback, VideoIndexingCallback>();

        // Event handlers
        services.AddScoped<IEventHandler<FileUploadedEvent>, FileUploadedVideoHandler>();
        services.AddScoped<IEventHandler<ResourceSharedEvent>, VideoSharedNotificationHandler>();

        // Transcoding configuration — bound from "Video:Transcoding" config section
        services.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var options = new VideoTranscodingOptions();
            config.GetSection("Video:Transcoding").Bind(options);
            return options;
        });

        return services;
    }

    /// <summary>
    /// Adds only the Video services needed by Blazor UI components rendered in Core.Server.
    /// Excludes background services and event handlers that should only run in the module host.
    /// </summary>
    public static IServiceCollection AddVideoUiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        DatabaseProvider provider,
        string connectionString)
    {
        // Register VideoDbContext for Blazor Server interactive rendering
        services.AddDbContext<VideoDbContext>(options =>
            ModuleDbContextConfiguration.Configure(options, provider, connectionString, "DotNetCloud.Modules.Video.Data.SqlServer"),
            ServiceLifetime.Transient);

        // Business services
        services.AddScoped<VideoService>();
        services.AddScoped<IVideoService>(sp => sp.GetRequiredService<VideoService>());
        services.AddScoped<VideoCollectionService>();
        services.AddScoped<IVideoCollectionService>(sp => sp.GetRequiredService<VideoCollectionService>());
        services.AddScoped<SubtitleService>();
        services.AddScoped<ISubtitleService>(sp => sp.GetRequiredService<SubtitleService>());
        services.AddScoped<VideoMetadataService>();
        services.AddScoped<IVideoMetadataService>(sp => sp.GetRequiredService<VideoMetadataService>());
        services.AddScoped<VideoStreamingService>();
        services.AddScoped<IVideoStreamingService>(sp => sp.GetRequiredService<VideoStreamingService>());
        services.AddScoped<VideoSeriesService>();
        services.AddScoped<IVideoSeriesService>(sp => sp.GetRequiredService<VideoSeriesService>());
        services.AddScoped<VideoSettingsProvider>();
        services.AddScoped<IVideoSettingsProvider>(sp => sp.GetRequiredService<VideoSettingsProvider>());

        // Thumbnail service
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

        // Background enrichment queue (singleton)
        services.AddSingleton<InMemoryVideoEnrichmentBackgroundQueue>();
        services.AddSingleton<IVideoEnrichmentBackgroundQueue>(sp => sp.GetRequiredService<InMemoryVideoEnrichmentBackgroundQueue>());

        // Scan progress state (singleton)
        services.AddSingleton<VideoScanProgressState>();

        // Indexing callback
        services.AddScoped<IVideoIndexingCallback, VideoIndexingCallback>();

        // NOTE: VideoEnrichmentBackgroundService (hosted) and event handlers
        // are NOT registered here — they run only in the module host process.

        return services;
    }
}
