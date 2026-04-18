using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Photos.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos;

/// <summary>
/// Photos module implementation.
/// Manages photo library, albums, non-destructive editing, and geo-clustering.
/// </summary>
public sealed class PhotosModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private FileUploadedPhotoHandler? _fileUploadedHandler;
    private ILogger<PhotosModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new PhotosModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<PhotosModule>>();
        _logger?.LogInformation("Initializing Photos module ({ModuleId})", context.ModuleId);

        _eventBus = context.Services.GetRequiredService<IEventBus>();

        var handlerLogger = context.Services.GetService<ILogger<FileUploadedPhotoHandler>>();
        var indexingCallback = context.Services.GetService<IPhotoIndexingCallback>();
        _fileUploadedHandler = new FileUploadedPhotoHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FileUploadedPhotoHandler>.Instance,
            indexingCallback);

        await _eventBus.SubscribeAsync(_fileUploadedHandler, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("Photos module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("Photos module started");
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

        _logger?.LogInformation("Photos module stopped");
    }

    /// <summary>
    /// Gets whether the module has been initialized.
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Gets whether the module is currently running.
    /// </summary>
    public bool IsRunning => _running;

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return new ValueTask(DisposeAsync());
    }
}
