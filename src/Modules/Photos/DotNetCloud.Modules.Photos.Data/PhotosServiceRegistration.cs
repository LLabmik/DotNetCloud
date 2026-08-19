using DotNetCloud.Core.Data.Extensions;
using DotNetCloud.Core.Data.Naming;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Photos.Data;
using DotNetCloud.Modules.Photos.Data.Services;
using DotNetCloud.Modules.Photos.Events;
using DotNetCloud.Modules.Photos.Services;
using Microsoft.EntityFrameworkCore;
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
        // Register Files data services (provides IDownloadService needed for full-size photo serving)
        services.AddFilesServices(configuration);

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

    /// <summary>
    /// Adds only the Photos services needed by Blazor UI components rendered in Core.Server.
    /// Excludes background services and event handlers that should only run in the module host.
    /// </summary>
    public static IServiceCollection AddPhotosUiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        DatabaseProvider provider,
        string connectionString)
    {
        // Register PhotosDbContext for Blazor Server interactive rendering
        services.AddDbContext<PhotosDbContext>(options =>
            ModuleDbContextConfiguration.Configure(options, provider, connectionString, "DotNetCloud.Modules.Photos.Data.SqlServer"),
            ServiceLifetime.Transient);

        // Business services
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

        // Indexing callback
        services.AddScoped<IPhotoIndexingCallback, PhotoIndexingCallback>();

        // NOTE: PhotoIndexingBackgroundService (hosted) and event handlers
        // are NOT registered here — they run only in the module host process.

        return services;
    }
}
