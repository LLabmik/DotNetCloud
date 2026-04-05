using DotNetCloud.Modules.Music.Data.Services;
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
        services.AddScoped<ArtistService>();
        services.AddScoped<MusicAlbumService>();
        services.AddScoped<TrackService>();
        services.AddScoped<PlaylistService>();
        services.AddScoped<PlaybackService>();
        services.AddScoped<RecommendationService>();
        services.AddScoped<EqPresetService>();
        services.AddScoped<LibraryScanService>();
        services.AddScoped<MusicMetadataService>();
        services.AddScoped<AlbumArtService>();
        services.AddScoped<MusicStreamingService>();

        return services;
    }
}
