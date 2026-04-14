using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Music.Data.Services;
using DotNetCloud.Modules.Music.Events;
using DotNetCloud.Modules.Music.Services;
using DotNetCloud.Modules.Music.UI;
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
        services.AddScoped<LibraryScanService>();
        services.AddScoped<MusicMetadataService>();
        services.AddScoped<AlbumArtService>();
        services.AddScoped<MusicStreamingService>();
        services.AddScoped<IMusicStreamingService>(sp => sp.GetRequiredService<MusicStreamingService>());

        // Shared playback state (survives page navigations within a circuit)
        services.AddScoped<MusicPlaybackState>();

        // Scan progress state (bridges IProgress callbacks to Blazor StateHasChanged)
        services.AddScoped<ScanProgressState>();

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

        services.AddScoped<MetadataEnrichmentService>();
        services.AddScoped<IMetadataEnrichmentService>(sp => sp.GetRequiredService<MetadataEnrichmentService>());

        // Indexing callback (bridges Module → Data for FileUploadedEvent handling)
        services.AddScoped<IMusicIndexingCallback, MusicIndexingCallback>();

        // Event handlers
        services.AddScoped<IEventHandler<FileUploadedEvent>, FileUploadedMusicHandler>();
        services.AddScoped<IEventHandler<ResourceSharedEvent>, PlaylistSharedNotificationHandler>();

        return services;
    }
}
