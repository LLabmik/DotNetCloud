using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Files.Events;
using DotNetCloud.Modules.Tracks.Events;
using DotNetCloud.Modules.Tracks.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNetCloud.Modules.Tracks;

/// <summary>
/// Tracks module implementation.
/// Manages kanban boards, cards, sprints, time tracking, and project management.
/// </summary>
public sealed class TracksModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private ILogger<TracksModule>? _logger;
    private bool _initialized;
    private bool _running;

    // Event handlers
    private TracksRealtimeEventHandler? _realtimeHandler;
    private FileDeletedEventHandler? _fileDeletedHandler;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new TracksModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<TracksModule>>();
        _logger?.LogInformation("Initializing Tracks module ({ModuleId})", context.ModuleId);

        _eventBus = context.Services.GetRequiredService<IEventBus>();

        // Register real-time event handler — broadcasts board state changes to connected clients
        var realtimeService = context.Services.GetRequiredService<ITracksRealtimeService>();
        _realtimeHandler = new TracksRealtimeEventHandler(
            realtimeService,
            context.Services.GetService<ILogger<TracksRealtimeEventHandler>>() ?? NullLogger<TracksRealtimeEventHandler>.Instance);

        await _eventBus.SubscribeAsync<CardCreatedEvent>(_realtimeHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CardUpdatedEvent>(_realtimeHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CardMovedEvent>(_realtimeHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CardDeletedEvent>(_realtimeHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CardAssignedEvent>(_realtimeHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CardCommentAddedEvent>(_realtimeHandler, cancellationToken);
        await _eventBus.SubscribeAsync<BoardCreatedEvent>(_realtimeHandler, cancellationToken);
        await _eventBus.SubscribeAsync<BoardDeletedEvent>(_realtimeHandler, cancellationToken);
        await _eventBus.SubscribeAsync<SprintStartedEvent>(_realtimeHandler, cancellationToken);
        await _eventBus.SubscribeAsync<SprintCompletedEvent>(_realtimeHandler, cancellationToken);
        await _eventBus.SubscribeAsync<TeamCreatedEvent>(_realtimeHandler, cancellationToken);
        await _eventBus.SubscribeAsync<TeamDeletedEvent>(_realtimeHandler, cancellationToken);

        // Register cross-module file cleanup handler
        _fileDeletedHandler = new FileDeletedEventHandler(
            context.Services,
            context.Services.GetService<ILogger<FileDeletedEventHandler>>() ?? NullLogger<FileDeletedEventHandler>.Instance);
        await _eventBus.SubscribeAsync(_fileDeletedHandler, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("Tracks module initialized successfully with real-time and cross-module event handlers");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("Tracks module started");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;

        // Unregister all event handlers
        if (_eventBus is not null)
        {
            if (_realtimeHandler is not null)
            {
                await _eventBus.UnsubscribeAsync<CardCreatedEvent>(_realtimeHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<CardUpdatedEvent>(_realtimeHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<CardMovedEvent>(_realtimeHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<CardDeletedEvent>(_realtimeHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<CardAssignedEvent>(_realtimeHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<CardCommentAddedEvent>(_realtimeHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<BoardCreatedEvent>(_realtimeHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<BoardDeletedEvent>(_realtimeHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<SprintStartedEvent>(_realtimeHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<SprintCompletedEvent>(_realtimeHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<TeamCreatedEvent>(_realtimeHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<TeamDeletedEvent>(_realtimeHandler, cancellationToken);
            }

            if (_fileDeletedHandler is not null)
            {
                await _eventBus.UnsubscribeAsync(_fileDeletedHandler, cancellationToken);
            }
        }

        _logger?.LogInformation("Tracks module stopped");
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("Tracks module disposed");
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
