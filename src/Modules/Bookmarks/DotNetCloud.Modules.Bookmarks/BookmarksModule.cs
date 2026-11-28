using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Bookmarks.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Bookmarks;

/// <summary>
/// Bookmarks module implementation.
/// Manages private bookmarks with folder organization, import/export, and rich preview scraping.
/// </summary>
public sealed class BookmarksModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private BookmarkCreatedEventHandler? _eventCreatedHandler;
    private ILogger<BookmarksModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new BookmarksModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<BookmarksModule>>();
        _logger?.LogInformation("Initializing Bookmarks module ({ModuleId})", context.ModuleId);

        _eventBus = context.Services.GetRequiredService<IEventBus>();

        var handlerLogger = context.Services.GetService<ILogger<BookmarkCreatedEventHandler>>();
        _eventCreatedHandler = new BookmarkCreatedEventHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<BookmarkCreatedEventHandler>.Instance);
        await _eventBus.SubscribeAsync(_eventCreatedHandler, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("Bookmarks module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            throw new InvalidOperationException("Module must be initialized before starting.");

        _running = true;
        _logger?.LogInformation("Bookmarks module started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;

        if (_eventBus is not null && _eventCreatedHandler is not null)
            await _eventBus.UnsubscribeAsync(_eventCreatedHandler, cancellationToken);

        _logger?.LogInformation("Bookmarks module stopped");
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("Bookmarks module disposed");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());

    /// <summary>Gets whether the module has been initialized.</summary>
    public bool IsInitialized => _initialized;

    /// <summary>Gets whether the module is currently running.</summary>
    public bool IsRunning => _running;
}
