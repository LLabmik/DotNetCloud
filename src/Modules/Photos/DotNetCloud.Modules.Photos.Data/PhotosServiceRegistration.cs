using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Events;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotNetCloud.Modules.Photos.Data;

/// <summary>
/// Registers Photos module services for dependency injection.
/// </summary>
public static class PhotosServiceRegistration
{
    /// <summary>
    /// Adds Photos module services to the DI container.
    /// </summary>
    /// <remarks>
    /// The module owns its own data store and reaches everything else through the core's module API,
    /// so nothing from the Files module is registered here (it used to be, which silently gave the
    /// module in-process access to another module's database and file storage).
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Host configuration.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddPhotosServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Business services (forward-registered for concrete + interface injection)
        services.AddScoped<PhotoService>();
        services.AddScoped<IPhotoService>(sp => sp.GetRequiredService<PhotoService>());
        services.AddScoped<AlbumService>();
        services.AddScoped<Photos.Services.IAlbumService>(sp => sp.GetRequiredService<AlbumService>());
        services.AddScoped<PhotoMetadataService>();
        services.AddScoped<PhotoGeoService>();
        services.AddScoped<IPhotoGeoService>(sp => sp.GetRequiredService<PhotoGeoService>());
        services.AddScoped<PhotoShareService>();
        services.AddScoped<IPhotoShareService>(sp => sp.GetRequiredService<PhotoShareService>());
        services.AddScoped<PhotoEditService>();
        services.AddScoped<IPhotoEditService>(sp => sp.GetRequiredService<PhotoEditService>());
        services.AddScoped<SlideshowService>();
        services.AddScoped<ISlideshowService>(sp => sp.GetRequiredService<SlideshowService>());
        services.AddScoped<PhotoThumbnailService>();
        services.AddScoped<IPhotoThumbnailService>(sp => sp.GetRequiredService<PhotoThumbnailService>());

        // Indexing callback (bridges Module → Data for the core's media import)
        services.AddScoped<IPhotoIndexingCallback, PhotoIndexingCallback>();

        // Event handlers (local events only — a module never sees another module's events)
        services.AddScoped<IEventHandler<AlbumSharedEvent>, AlbumSharedNotificationHandler>();

        return services;
    }
}
