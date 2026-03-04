using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Files.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Files;

/// <summary>
/// Files module implementation.
/// Manages file storage, versioning, sharing, quotas, and trash operations.
/// </summary>
/// <remarks>
/// The Files module is the primary public-facing feature of DotNetCloud.
/// It provides:
/// <list type="bullet">
///   <item><description>File and folder management with tree structure</description></item>
///   <item><description>Chunked upload/download with content-hash deduplication</description></item>
///   <item><description>File versioning and restore</description></item>
///   <item><description>Sharing with users, teams, groups, and public links</description></item>
///   <item><description>Storage quotas per user</description></item>
///   <item><description>Trash bin with restore capability</description></item>
///   <item><description>Tags and comments for collaboration</description></item>
/// </list>
/// </remarks>
public sealed class FilesModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private FileUploadedEventHandler? _fileUploadedHandler;
    private ILogger<FilesModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new FilesModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<FilesModule>>();
        _logger?.LogInformation("Initializing Files module ({ModuleId})", context.ModuleId);

        // Resolve the event bus from DI
        _eventBus = context.Services.GetRequiredService<IEventBus>();

        // Create and register event handler
        var handlerLogger = context.Services.GetService<ILogger<FileUploadedEventHandler>>();
        _fileUploadedHandler = new FileUploadedEventHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FileUploadedEventHandler>.Instance);

        await _eventBus.SubscribeAsync(_fileUploadedHandler, cancellationToken);

        // Load module-specific configuration
        if (context.Configuration.TryGetValue("storage_path", out var storagePathObj))
        {
            _logger?.LogInformation("Storage path configured: {StoragePath}", storagePathObj);
        }

        if (context.Configuration.TryGetValue("max_upload_size", out var maxUploadObj))
        {
            _logger?.LogInformation("Max upload size configured: {MaxUploadSize}", maxUploadObj);
        }

        _initialized = true;
        _logger?.LogInformation("Files module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("Files module started");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;

        if (_eventBus is not null && _fileUploadedHandler is not null)
        {
            await _eventBus.UnsubscribeAsync(_fileUploadedHandler, cancellationToken);
        }

        _logger?.LogInformation("Files module stopped");
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("Files module disposed");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return new ValueTask(DisposeAsync());
    }

    /// <summary>Gets whether the module has been initialized.</summary>
    public bool IsInitialized => _initialized;

    /// <summary>Gets whether the module is currently running.</summary>
    public bool IsRunning => _running;
}
