using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Photos;

/// <summary>
/// Photos module implementation.
/// Manages photo library, albums, non-destructive editing, and geo-clustering.
/// </summary>
/// <remarks>
/// The module used to subscribe to the Files module's <c>FileUploadedEvent</c> in-process. A
/// sandboxed module cannot see another module's events, so images are indexed by the core's media
/// import (which calls this module's <c>IndexPhoto</c> RPC) and by the module's own library scan.
/// </remarks>
public sealed class PhotosModule : IModuleLifecycle
{
    private ILogger<PhotosModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new PhotosModuleManifest();

    /// <inheritdoc />
    public Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<PhotosModule>>();
        _logger?.LogInformation("Initializing Photos module ({ModuleId})", context.ModuleId);

        _initialized = true;
        _logger?.LogInformation("Photos module initialized successfully");
        return Task.CompletedTask;
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
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;
        _logger?.LogInformation("Photos module stopped");
        return Task.CompletedTask;
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
