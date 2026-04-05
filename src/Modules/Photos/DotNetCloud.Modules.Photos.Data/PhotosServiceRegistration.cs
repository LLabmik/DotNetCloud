using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Events;
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
    public static IServiceCollection AddPhotosServices(this IServiceCollection services, IConfiguration configuration)
    {
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

        // Indexing callback (bridges Module → Data for FileUploadedEvent handling)
        services.AddScoped<IPhotoIndexingCallback, PhotoIndexingCallback>();

        // Event handlers
        services.AddScoped<IEventHandler<FileUploadedEvent>, FileUploadedPhotoHandler>();
        services.AddScoped<IEventHandler<AlbumSharedEvent>, AlbumSharedNotificationHandler>();

        // Background services
        services.AddSingleton<IHostedService, PhotoIndexingBackgroundService>();

        return services;
    }
}
