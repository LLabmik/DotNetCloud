using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.AI.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.AI;

/// <summary>
/// AI Assistant module implementation.
/// Provides LLM-powered chat, summarization, and content generation
/// using local (Ollama) or cloud-based providers.
/// </summary>
public sealed class AiModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private ConversationCreatedEventHandler? _conversationCreatedHandler;
    private ILogger<AiModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new AiModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<AiModule>>();
        _logger?.LogInformation("Initializing AI module ({ModuleId})", context.ModuleId);

        _eventBus = context.Services.GetRequiredService<IEventBus>();

        var handlerLogger = context.Services.GetService<ILogger<ConversationCreatedEventHandler>>();
        _conversationCreatedHandler = new ConversationCreatedEventHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ConversationCreatedEventHandler>.Instance);
        await _eventBus.SubscribeAsync(_conversationCreatedHandler, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("AI module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("AI module started");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;

        if (_eventBus is not null && _conversationCreatedHandler is not null)
        {
            await _eventBus.UnsubscribeAsync(_conversationCreatedHandler, cancellationToken);
        }

        _logger?.LogInformation("AI module stopped");
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("AI module disposed");
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
