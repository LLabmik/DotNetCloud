using DotNetCloud.Core.Capabilities;
using DotNetCloud.Core.DTOs.Media;
using Microsoft.Extensions.DependencyInjection;

namespace DotNetCloud.Core.ServiceDefaults.Media;

/// <summary>
/// Extension methods for registering media metadata extractors in the DI container.
/// </summary>
public static class MediaServiceCollectionExtensions
{
    /// <summary>
    /// Registers the built-in media metadata extractors (EXIF, Audio, Video)
    /// as keyed services by <see cref="MediaType"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMediaMetadataExtractors(this IServiceCollection services)
    {
        // Register as keyed services by MediaType for targeted resolution
        services.AddSingleton<ExifMetadataExtractor>();
        services.AddKeyedSingleton<IMediaMetadataExtractor, ExifMetadataExtractor>(MediaType.Photo,
            (sp, _) => sp.GetRequiredService<ExifMetadataExtractor>());

        services.AddSingleton<AudioMetadataExtractor>();
        services.AddKeyedSingleton<IMediaMetadataExtractor, AudioMetadataExtractor>(MediaType.Audio,
            (sp, _) => sp.GetRequiredService<AudioMetadataExtractor>());

        services.AddSingleton<VideoMetadataExtractor>();
        services.AddKeyedSingleton<IMediaMetadataExtractor, VideoMetadataExtractor>(MediaType.Video,
            (sp, _) => sp.GetRequiredService<VideoMetadataExtractor>());

        // Also register all extractors in an enumerable for iteration
        services.AddSingleton<IMediaMetadataExtractor>(sp => sp.GetRequiredService<ExifMetadataExtractor>());
        services.AddSingleton<IMediaMetadataExtractor>(sp => sp.GetRequiredService<AudioMetadataExtractor>());
        services.AddSingleton<IMediaMetadataExtractor>(sp => sp.GetRequiredService<VideoMetadataExtractor>());

        return services;
    }
}
