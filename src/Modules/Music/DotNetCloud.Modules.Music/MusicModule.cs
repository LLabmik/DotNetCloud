using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Music.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Music;

/// <summary>
/// Music module implementation.
/// Manages music library, playlists, streaming, and Subsonic API compatibility.
/// </summary>
public sealed class MusicModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private FileUploadedMusicHandler? _fileUploadedHandler;
    private ILogger<MusicModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new MusicModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<MusicModule>>();
        _logger?.LogInformation("Initializing Music module ({ModuleId})", context.ModuleId);

        _eventBus = context.Services.GetRequiredService<IEventBus>();

        var handlerLogger = context.Services.GetService<ILogger<FileUploadedMusicHandler>>();
        var indexingCallback = context.Services.GetService<IMusicIndexingCallback>();
        _fileUploadedHandler = new FileUploadedMusicHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FileUploadedMusicHandler>.Instance,
            indexingCallback);

        await _eventBus.SubscribeAsync(_fileUploadedHandler, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("Music module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("Music module started");
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

        _logger?.LogInformation("Music module stopped");
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
