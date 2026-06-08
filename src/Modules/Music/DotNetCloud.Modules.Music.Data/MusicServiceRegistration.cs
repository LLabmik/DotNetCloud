using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Storage;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Music.Data;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Events;
using DotNetCloud.Modules.Music.Services;
using DotNetCloud.Modules.Music.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Modules.Music.Data;

/// <summary>
/// Registers Music module services for dependency injection.
/// </summary>
public static class MusicServiceRegistration
{
    /// <summary>
    /// Adds Music module services to the DI container.
    /// </summary>
    public static IServiceCollection AddMusicServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Files data services (provides IDownloadService needed by MusicAlbumService)
        services.AddFilesServices(configuration);

        // Business services (forward-registered for concrete + interface injection)
        services.AddScoped<ArtistService>();
        services.AddScoped<IArtistService>(sp => sp.GetRequiredService<ArtistService>());
        services.AddScoped<MusicAlbumService>();
        services.AddScoped<IMusicAlbumService>(sp => sp.GetRequiredService<MusicAlbumService>());
        services.AddScoped<TrackService>();
        services.AddScoped<ITrackService>(sp => sp.GetRequiredService<TrackService>());
        services.AddScoped<PlaylistService>();
        services.AddScoped<Music.Services.IPlaylistService>(sp => sp.GetRequiredService<PlaylistService>());
        services.AddScoped<PlaybackService>();
        services.AddScoped<IPlaybackService>(sp => sp.GetRequiredService<PlaybackService>());
        services.AddScoped<RecommendationService>();
        services.AddScoped<IRecommendationService>(sp => sp.GetRequiredService<RecommendationService>());
        services.AddScoped<EqPresetService>();
        services.AddScoped<IEqPresetService>(sp => sp.GetRequiredService<EqPresetService>());
        // Content-addressed storage for binary assets (album art)
        var storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        var mediaCachePath = configuration["Files:Storage:MediaCachePath"] ?? Path.Combine(storageRoot, ".media-cache");
        services.AddSingleton(new ContentAddressedStorage(mediaCachePath));

        services.AddScoped<LibraryScanService>();
        services.AddScoped<MusicMetadataService>();
        services.AddScoped<AlbumArtService>();
        services.AddScoped<MusicStreamingService>();
        services.AddScoped<IMusicStreamingService>(sp => sp.GetRequiredService<MusicStreamingService>());

        // Shared playback state (survives page navigations within a circuit)
        services.AddScoped<MusicPlaybackState>();

        // Active playlist context (tracks which playlist the current playback originates from)
        services.AddScoped<ActivePlaylistContext>();

        // Shared per-user scan and enrichment progress state
        services.AddSingleton<ScanProgressState>();

        // MusicBrainz + Cover Art Archive enrichment services
        var rateLimitMs = configuration.GetValue("Music:Enrichment:RateLimitMs", 1100);
        services.AddSingleton(new MusicBrainzRateLimiter(rateLimitMs));

        services.AddHttpClient<IMusicBrainzClient, MusicBrainzClient>(client =>
        {
            client.BaseAddress = new Uri("https://musicbrainz.org/ws/2/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetCloud/0.2.0 (https://github.com/LLabmik/DotNetCloud)");
        });

        services.AddHttpClient<ICoverArtArchiveClient, CoverArtArchiveClient>(client =>
        {
            client.BaseAddress = new Uri("https://coverartarchive.org/");
        });

        services.AddHttpClient<IAudioDbClient, AudioDbClient>(client =>
        {
            client.BaseAddress = new Uri("https://www.theaudiodb.com/api/v1/json/123/");
        });

        services.AddScoped<MetadataEnrichmentService>();
        services.AddScoped<IMetadataEnrichmentService>(sp => sp.GetRequiredService<MetadataEnrichmentService>());
        services.AddSingleton<InMemoryMusicEnrichmentBackgroundQueue>();
        services.AddSingleton<IMusicEnrichmentBackgroundQueue>(sp => sp.GetRequiredService<InMemoryMusicEnrichmentBackgroundQueue>());
        services.AddHostedService<MusicEnrichmentBackgroundService>();

        // Indexing callback (bridges Module → Data for FileUploadedEvent handling)
        services.AddScoped<IMusicIndexingCallback, MusicIndexingCallback>();

        // Event handlers
        services.AddScoped<IEventHandler<FileUploadedEvent>, FileUploadedMusicHandler>();
        services.AddScoped<IEventHandler<ResourceSharedEvent>, PlaylistSharedNotificationHandler>();

        return services;
    }

    /// <summary>
    /// Adds only the Music services needed by Blazor UI components rendered in Core.Server.
    /// Excludes background services and event handlers that should only run in the module host.
    /// </summary>
    public static IServiceCollection AddMusicUiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        DatabaseProvider provider,
        string connectionString)
    {
        // Register MusicDbContext for Blazor Server interactive rendering
        services.AddDbContextFactory<MusicDbContext>(options =>
            ModuleDbContextConfiguration.Configure(options, provider, connectionString, "DotNetCloud.Modules.Music.Data.SqlServer"));
        services.AddDbContext<MusicDbContext>(options =>
            ModuleDbContextConfiguration.Configure(options, provider, connectionString, "DotNetCloud.Modules.Music.Data.SqlServer"),
            ServiceLifetime.Transient);

        // Business services
        services.AddScoped<ArtistService>();
        services.AddScoped<IArtistService>(sp => sp.GetRequiredService<ArtistService>());
        services.AddScoped<MusicAlbumService>();
        services.AddScoped<IMusicAlbumService>(sp => sp.GetRequiredService<MusicAlbumService>());
        services.AddScoped<TrackService>();
        services.AddScoped<ITrackService>(sp => sp.GetRequiredService<TrackService>());
        services.AddScoped<PlaylistService>();
        services.AddScoped<Music.Services.IPlaylistService>(sp => sp.GetRequiredService<PlaylistService>());
        services.AddScoped<PlaybackService>();
        services.AddScoped<IPlaybackService>(sp => sp.GetRequiredService<PlaybackService>());
        services.AddScoped<RecommendationService>();
        services.AddScoped<IRecommendationService>(sp => sp.GetRequiredService<RecommendationService>());
        services.AddScoped<EqPresetService>();
        services.AddScoped<IEqPresetService>(sp => sp.GetRequiredService<EqPresetService>());

        // Content-addressed storage for binary assets (album art)
        var storageRoot = configuration["Files:Storage:RootPath"] ?? Path.GetTempPath();
        var mediaCachePath = configuration["Files:Storage:MediaCachePath"] ?? Path.Combine(storageRoot, ".media-cache");
        services.AddSingleton(new ContentAddressedStorage(mediaCachePath));

        services.AddScoped<LibraryScanService>();
        services.AddScoped<MusicMetadataService>();
        services.AddScoped<AlbumArtService>();
        services.AddScoped<MusicStreamingService>();
        services.AddScoped<IMusicStreamingService>(sp => sp.GetRequiredService<MusicStreamingService>());

        // Shared playback state (survives page navigations within a circuit)
        services.AddScoped<MusicPlaybackState>();
        services.AddScoped<ActivePlaylistContext>();

        // Shared per-user scan and enrichment progress state
        services.AddSingleton<ScanProgressState>();

        // MusicBrainz + Cover Art Archive enrichment services
        var rateLimitMs = configuration.GetValue("Music:Enrichment:RateLimitMs", 1100);
        services.AddSingleton(new MusicBrainzRateLimiter(rateLimitMs));

        services.AddHttpClient<IMusicBrainzClient, MusicBrainzClient>(client =>
        {
            client.BaseAddress = new Uri("https://musicbrainz.org/ws/2/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DotNetCloud/0.2.0 (https://github.com/LLabmik/DotNetCloud)");
        });

        services.AddHttpClient<ICoverArtArchiveClient, CoverArtArchiveClient>(client =>
        {
            client.BaseAddress = new Uri("https://coverartarchive.org/");
        });

        services.AddHttpClient<IAudioDbClient, AudioDbClient>(client =>
        {
            client.BaseAddress = new Uri("https://www.theaudiodb.com/api/v1/json/123/");
        });

        services.AddScoped<MetadataEnrichmentService>();
        services.AddScoped<IMetadataEnrichmentService>(sp => sp.GetRequiredService<MetadataEnrichmentService>());
        services.AddSingleton<InMemoryMusicEnrichmentBackgroundQueue>();
        services.AddSingleton<IMusicEnrichmentBackgroundQueue>(sp => sp.GetRequiredService<InMemoryMusicEnrichmentBackgroundQueue>());

        // Indexing callback
        services.AddScoped<IMusicIndexingCallback, MusicIndexingCallback>();

        // NOTE: MusicEnrichmentBackgroundService (hosted) and event handlers
        // are NOT registered here — they run only in the module host process.

        return services;
    }
}
