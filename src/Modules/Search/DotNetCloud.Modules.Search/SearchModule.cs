using DotNetCloud.Core.Events;
using DotNetCloud.Core.Events.Search;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Search.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Search;

/// <summary>
/// Search module implementation.
/// Provides cross-module full-text search using native database FTS capabilities.
/// </summary>
/// <remarks>
/// The Search module:
/// <list type="bullet">
///   <item><description>Maintains a centralized search index across all modules</description></item>
///   <item><description>Subscribes to <see cref="SearchIndexRequestEvent"/> for real-time indexing</description></item>
///   <item><description>Supports scheduled full reindex operations</description></item>
///   <item><description>Provides content extraction for PDF, DOCX, XLSX, and text files</description></item>
/// </list>
/// </remarks>
public sealed class SearchModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private SearchIndexRequestEventHandler? _indexRequestHandler;
    private ILogger<SearchModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new SearchModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<SearchModule>>();
        _logger?.LogInformation("Initializing Search module ({ModuleId})", context.ModuleId);

        // Resolve the event bus from DI
        _eventBus = context.Services.GetRequiredService<IEventBus>();

        // Create and register event handler for search index requests
        var handlerLogger = context.Services.GetService<ILogger<SearchIndexRequestEventHandler>>();
        var searchProvider = context.Services.GetService<Core.Capabilities.ISearchProvider>();

        _indexRequestHandler = new SearchIndexRequestEventHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SearchIndexRequestEventHandler>.Instance,
            searchProvider);

        await _eventBus.SubscribeAsync(_indexRequestHandler, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("Search module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("Search module started");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;

        if (_eventBus is not null && _indexRequestHandler is not null)
        {
            await _eventBus.UnsubscribeAsync(_indexRequestHandler, cancellationToken);
        }

        _logger?.LogInformation("Search module stopped");
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("Search module disposed");
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
