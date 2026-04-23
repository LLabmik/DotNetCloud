using DotNetCloud.Core.Events;
using DotNetCloud.Core.Services;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DotNetCloud.Modules.Files.Data;

/// <summary>
/// Extension methods for registering Files module services in the DI container.
/// </summary>
public static class FilesServiceRegistration
{
    /// <summary>
    /// Registers all Files module service implementations in the DI container.
    /// </summary>
    public static IServiceCollection AddFilesServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.TryAddSingleton<IBackgroundServiceTracker, BackgroundServiceTracker>();

        // Bind options from configuration or use defaults
        if (configuration is not null)
        {
            services.Configure<VersionRetentionOptions>(configuration.GetSection(VersionRetentionOptions.SectionName));
            services.Configure<TrashRetentionOptions>(configuration.GetSection(TrashRetentionOptions.SectionName));
            services.Configure<QuotaOptions>(configuration.GetSection(QuotaOptions.SectionName));
            services.Configure<CollaboraOptions>(configuration.GetSection(CollaboraOptions.SectionName));
            services.Configure<FileUploadOptions>(configuration.GetSection(FileUploadOptions.SectionName));
            services.Configure<FileSystemOptions>(configuration.GetSection(FileSystemOptions.SectionName));
        }
        else
        {
            services.Configure<VersionRetentionOptions>(_ => { }); // use defaults
            services.Configure<TrashRetentionOptions>(_ => { }); // use defaults
            services.Configure<QuotaOptions>(_ => { }); // use defaults
            services.Configure<CollaboraOptions>(_ => { }); // use defaults
            services.Configure<FileUploadOptions>(_ => { }); // use defaults
            services.Configure<FileSystemOptions>(_ => { }); // use defaults
        }

        // File scanner (NoOp until a real scanner such as ClamAV is integrated)
        services.AddSingleton<IFileScanner, NoOpFileScanner>();

        // Database-backed services (Scoped)
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IChunkedUploadService, ChunkedUploadService>();
        services.AddScoped<IDownloadService, DownloadService>();
        services.AddScoped<IVersionService, VersionService>();
        services.AddScoped<IShareService, ShareService>();
        services.AddScoped<ITrashService, TrashService>();
        services.AddScoped<IQuotaService, QuotaService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<ISyncDeviceResolver, SyncDeviceResolver>();
        services.AddScoped<IDeviceContext, DeviceContext>();
        services.AddSingleton<ISyncChangeNotifier, SyncChangeNotifier>();
        services.AddScoped<IStorageMetricsService, StorageMetricsService>();
        services.AddSingleton<IVideoFrameExtractor, FfmpegVideoFrameExtractor>();
        services.AddSingleton<IPdfPageRenderer, PdftoppmPdfPageRenderer>();
        services.AddSingleton<IThumbnailService, ThumbnailService>();

        // Event-driven thumbnail generation for uploaded images
        services.AddSingleton<FileUploadedThumbnailHandler>();
        services.AddSingleton<IEventHandler<FileUploadedEvent>>(sp =>
            sp.GetRequiredService<FileUploadedThumbnailHandler>());

        // WOPI / Collabora services (Scoped for DB access)
        services.AddScoped<IWopiService, WopiService>();
        services.AddScoped<IWopiTokenService, WopiTokenService>();
        services.AddScoped<IWopiProofKeyValidator, WopiProofKeyValidator>();

        // Collabora discovery service (Singleton — caches discovery results)
        services.AddSingleton<ICollaboraDiscoveryService, CollaboraDiscoveryService>();

        // Session tracker (Singleton — in-memory concurrent session enforcement)
        services.AddSingleton<IWopiSessionTracker, WopiSessionTracker>();

        // HTTP client for Collabora discovery
        services.AddHttpClient("Collabora")
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var options = sp.GetRequiredService<IOptions<CollaboraOptions>>().Value;
                var handler = new HttpClientHandler();

                if (options.AllowInsecureTls)
                {
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                }

                return handler;
            });

        // Health check for Collabora
        services.AddHealthChecks()
            .AddCheck<CollaboraHealthCheck>("collabora_online");

        // Collabora process manager (singleton BackgroundService — manages built-in CODE process)
        services.AddSingleton<CollaboraProcessManager>();
        services.AddSingleton<ICollaboraProcessManager>(sp => sp.GetRequiredService<CollaboraProcessManager>());
        services.AddHostedService(sp => sp.GetRequiredService<CollaboraProcessManager>());

        // Background services
        services.AddHostedService<UploadSessionCleanupService>();
        services.AddHostedService<TrashCleanupService>();
        services.AddHostedService<QuotaRecalculationService>();
        services.AddHostedService<VersionCleanupService>();
        services.AddHostedService<TempFileCleanupService>();
        services.AddHostedService<ShareExpiryNotificationService>();
        services.AddHostedService<ExpiredShareCleanupService>();

        return services;
    }
}
