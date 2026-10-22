using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Files.Data.Services;
using DotNetCloud.Modules.Files.Options;
using DotNetCloud.Modules.Files.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files.Data;

/// <summary>
/// Registers the default-content services needed by the <c>dotnetcloud files default-content</c>
/// CLI commands. The CLI talks to the database and storage directly; the module host is not
/// required (or started).
/// </summary>
public static class FilesDefaultContentCliRegistration
{
    /// <summary>
    /// Adds the template store, importer, provisioner, queue, archive and admin facade.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="dataDirectory">The deployment's data directory (for <c>storage/</c>).</param>
    /// <param name="configuration">Optional configuration for <c>Files:</c> option binding.</param>
    public static IServiceCollection AddDefaultContentCliServices(
        this IServiceCollection services,
        string dataDirectory,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        services.AddLogging();

        if (configuration is not null)
        {
            services.Configure<DefaultUserContentOptions>(configuration.GetSection(DefaultUserContentOptions.SectionName));
            services.Configure<QuotaOptions>(configuration.GetSection(QuotaOptions.SectionName));
            services.Configure<FileUploadOptions>(configuration.GetSection(FileUploadOptions.SectionName));
            services.Configure<FileSystemOptions>(configuration.GetSection(FileSystemOptions.SectionName));
            services.Configure<VersionRetentionOptions>(configuration.GetSection(VersionRetentionOptions.SectionName));
            services.Configure<AdminSharedFolderOptions>(configuration.GetSection(AdminSharedFolderOptions.SectionName));
        }
        else
        {
            services.Configure<DefaultUserContentOptions>(_ => { });
            services.Configure<QuotaOptions>(_ => { });
            services.Configure<FileUploadOptions>(_ => { });
            services.Configure<FileSystemOptions>(_ => { });
            services.Configure<VersionRetentionOptions>(_ => { });
            services.Configure<AdminSharedFolderOptions>(_ => { });
        }

        var storagePath = Path.Combine(dataDirectory, "storage");
        services.AddSingleton<IFileStorageEngine>(sp =>
            new LocalFileStorageEngine(storagePath, sp.GetRequiredService<ILogger<LocalFileStorageEngine>>()));

        services.AddSingleton<IEventBus, NoOpEventBus>();
        services.AddSingleton<ISyncChangeNotifier, NoOpSyncChangeNotifier>();
        services.AddSingleton<IDefaultUserContentSettingsProvider, DefaultUserContentSettingsProvider>();
        services.AddSingleton<DefaultUserContentStore>();
        services.AddScoped<IMediaCaptureTimeService, MediaCaptureTimeService>();
        services.AddScoped<IQuotaService, QuotaService>();
        services.AddScoped<ITrashService, TrashService>();
        services.AddScoped<FileImportService>();
        services.AddScoped<DefaultUserContentProvisioner>();
        services.AddScoped<DefaultUserContentQueue>();
        services.AddScoped<DefaultUserContentAdminService>();
        services.AddScoped<DefaultUserContentArchive>();

        return services;
    }

    private sealed class NoOpEventBus : IEventBus
    {
        public Task PublishAsync<TEvent>(TEvent @event, CallerContext caller, CancellationToken cancellationToken = default)
            where TEvent : IEvent => Task.CompletedTask;

        public Task SubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
            where TEvent : IEvent => Task.CompletedTask;

        public Task UnsubscribeAsync<TEvent>(IEventHandler<TEvent> handler, CancellationToken cancellationToken = default)
            where TEvent : IEvent => Task.CompletedTask;
    }

    private sealed class NoOpSyncChangeNotifier : ISyncChangeNotifier
    {
        public Task NotifyAsync(Guid userId, long latestSequence, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public async IAsyncEnumerable<SyncChangeNotification> SubscribeAsync(
            Guid userId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public int GetConnectionCount(Guid userId) => 0;
    }
}
