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

        _initialized = true;
        _logger?.LogInformation("Chat module initialized successfully");
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
