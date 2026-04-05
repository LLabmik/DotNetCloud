using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Video.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Video;

/// <summary>
/// Video module implementation.
/// Manages video library, collections, subtitles, and streaming.
/// </summary>
public sealed class VideoModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private FileUploadedVideoHandler? _fileUploadedHandler;
    private ILogger<VideoModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new VideoModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<VideoModule>>();
        _logger?.LogInformation("Initializing Video module ({ModuleId})", context.ModuleId);

        _eventBus = context.Services.GetRequiredService<IEventBus>();

        var handlerLogger = context.Services.GetService<ILogger<FileUploadedVideoHandler>>();
        _fileUploadedHandler = new FileUploadedVideoHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FileUploadedVideoHandler>.Instance);

        await _eventBus.SubscribeAsync(_fileUploadedHandler, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("Video module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("Video module started");
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

        _logger?.LogInformation("Video module stopped");
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
