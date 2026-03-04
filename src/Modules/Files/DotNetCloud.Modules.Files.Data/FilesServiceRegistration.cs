using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Data.Services.Background;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        // Bind options from configuration or use defaults
        if (configuration is not null)
        {
            services.Configure<VersionRetentionOptions>(configuration.GetSection(VersionRetentionOptions.SectionName));
            services.Configure<TrashRetentionOptions>(configuration.GetSection(TrashRetentionOptions.SectionName));
            services.Configure<QuotaOptions>(configuration.GetSection(QuotaOptions.SectionName));
            services.Configure<CollaboraOptions>(configuration.GetSection(CollaboraOptions.SectionName));
        }
        else
        {
            services.Configure<VersionRetentionOptions>(_ => { }); // use defaults
            services.Configure<TrashRetentionOptions>(_ => { }); // use defaults
            services.Configure<QuotaOptions>(_ => { }); // use defaults
            services.Configure<CollaboraOptions>(_ => { }); // use defaults
        }

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
        services.AddScoped<IStorageMetricsService, StorageMetricsService>();

        // WOPI / Collabora services (Scoped for DB access)
        services.AddScoped<IWopiService, WopiService>();
        services.AddScoped<IWopiTokenService, WopiTokenService>();

        // Collabora discovery service (Singleton — caches discovery results)
        services.AddSingleton<ICollaboraDiscoveryService, CollaboraDiscoveryService>();

        // HTTP client for Collabora discovery
        services.AddHttpClient("Collabora");

        // Health check for Collabora
        services.AddHealthChecks()
            .AddCheck<CollaboraHealthCheck>("collabora_online");

        // Background services
        services.AddHostedService<UploadSessionCleanupService>();
        services.AddHostedService<TrashCleanupService>();
        services.AddHostedService<QuotaRecalculationService>();
        services.AddHostedService<VersionCleanupService>();

        return services;
    }
}
