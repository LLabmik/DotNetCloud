using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Chat.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Chat;

/// <summary>
/// Chat module implementation.
/// Manages real-time messaging, channels, direct messages, reactions, and presence.
/// </summary>
/// <remarks>
/// The Chat module provides:
/// <list type="bullet">
///   <item><description>Public, private, and direct-message channels</description></item>
///   <item><description>Real-time messaging with Markdown support</description></item>
///   <item><description>Typing indicators and user presence</description></item>
///   <item><description>Emoji reactions and message pinning</description></item>
///   <item><description>File sharing via cross-module integration with Files</description></item>
///   <item><description>@mention notifications</description></item>
/// </list>
/// </remarks>
public sealed class ChatModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private MessageSentEventHandler? _messageSentHandler;
    private ChannelCreatedEventHandler? _channelCreatedHandler;
    private TracksActivityChatHandler? _tracksActivityHandler;
    private IEventHandler<VideoCallInitiatedEvent>? _callNotificationInitiatedHandler;
    private IEventHandler<VideoCallMissedEvent>? _callNotificationMissedHandler;
    private IEventHandler<VideoCallEndedEvent>? _callNotificationEndedHandler;
    private ILogger<ChatModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new ChatModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<ChatModule>>();
        _logger?.LogInformation("Initializing Chat module ({ModuleId})", context.ModuleId);

        // Resolve the event bus from DI
        _eventBus = context.Services.GetRequiredService<IEventBus>();

        // Create and register event handlers
        var messageSentLogger = context.Services.GetService<ILogger<MessageSentEventHandler>>();
        _messageSentHandler = new MessageSentEventHandler(
            messageSentLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MessageSentEventHandler>.Instance);
        await _eventBus.SubscribeAsync(_messageSentHandler, cancellationToken);

        var channelCreatedLogger = context.Services.GetService<ILogger<ChannelCreatedEventHandler>>();
        _channelCreatedHandler = new ChannelCreatedEventHandler(
            channelCreatedLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ChannelCreatedEventHandler>.Instance);
        await _eventBus.SubscribeAsync(_channelCreatedHandler, cancellationToken);

        // Register cross-module Tracks activity handler
        var broadcaster = context.Services.GetService<Core.Capabilities.IRealtimeBroadcaster>();
        _tracksActivityHandler = new TracksActivityChatHandler(
            context.Services.GetService<ILogger<TracksActivityChatHandler>>()
                ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TracksActivityChatHandler>.Instance,
            broadcaster);
        await _eventBus.SubscribeAsync<CardCreatedEvent>(_tracksActivityHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CardMovedEvent>(_tracksActivityHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CardUpdatedEvent>(_tracksActivityHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CardDeletedEvent>(_tracksActivityHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CardAssignedEvent>(_tracksActivityHandler, cancellationToken);
        await _eventBus.SubscribeAsync<CardCommentAddedEvent>(_tracksActivityHandler, cancellationToken);
        await _eventBus.SubscribeAsync<SprintStartedEvent>(_tracksActivityHandler, cancellationToken);
        await _eventBus.SubscribeAsync<SprintCompletedEvent>(_tracksActivityHandler, cancellationToken);
        await _eventBus.SubscribeAsync<BoardCreatedEvent>(_tracksActivityHandler, cancellationToken);
        await _eventBus.SubscribeAsync<BoardDeletedEvent>(_tracksActivityHandler, cancellationToken);

        // Register call notification push handlers
        var callNotificationHandler = context.Services.GetService<Services.ICallNotificationHandler>();
        if (callNotificationHandler is not null)
        {
            _callNotificationInitiatedHandler = callNotificationHandler;
            _callNotificationMissedHandler = callNotificationHandler;
            _callNotificationEndedHandler = callNotificationHandler;
            await _eventBus.SubscribeAsync<VideoCallInitiatedEvent>(_callNotificationInitiatedHandler, cancellationToken);
            await _eventBus.SubscribeAsync<VideoCallMissedEvent>(_callNotificationMissedHandler, cancellationToken);
            await _eventBus.SubscribeAsync<VideoCallEndedEvent>(_callNotificationEndedHandler, cancellationToken);
        }

        _initialized = true;
        _logger?.LogInformation("Chat module initialized successfully with Tracks integration");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("Chat module started");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;

        if (_eventBus is not null)
        {
            if (_messageSentHandler is not null)
            {
                await _eventBus.UnsubscribeAsync(_messageSentHandler, cancellationToken);
            }

            if (_channelCreatedHandler is not null)
            {
                await _eventBus.UnsubscribeAsync(_channelCreatedHandler, cancellationToken);
            }

            if (_tracksActivityHandler is not null)
            {
                await _eventBus.UnsubscribeAsync<CardCreatedEvent>(_tracksActivityHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<CardMovedEvent>(_tracksActivityHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<CardUpdatedEvent>(_tracksActivityHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<CardDeletedEvent>(_tracksActivityHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<CardAssignedEvent>(_tracksActivityHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<CardCommentAddedEvent>(_tracksActivityHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<SprintStartedEvent>(_tracksActivityHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<SprintCompletedEvent>(_tracksActivityHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<BoardCreatedEvent>(_tracksActivityHandler, cancellationToken);
                await _eventBus.UnsubscribeAsync<BoardDeletedEvent>(_tracksActivityHandler, cancellationToken);
            }

            if (_callNotificationInitiatedHandler is not null)
                await _eventBus.UnsubscribeAsync<VideoCallInitiatedEvent>(_callNotificationInitiatedHandler, cancellationToken);
            if (_callNotificationMissedHandler is not null)
                await _eventBus.UnsubscribeAsync<VideoCallMissedEvent>(_callNotificationMissedHandler, cancellationToken);
            if (_callNotificationEndedHandler is not null)
                await _eventBus.UnsubscribeAsync<VideoCallEndedEvent>(_callNotificationEndedHandler, cancellationToken);
        }

        _logger?.LogInformation("Chat module stopped");
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("Chat module disposed");
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
