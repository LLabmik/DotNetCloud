using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Email.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Email;

/// <summary>
/// Email module implementation.
/// Manages email accounts, sync, send/compose, threading, and server-side rules/filters.
/// </summary>
public sealed class EmailModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private EmailAccountAddedEventHandler? _eventAccountAddedHandler;
    private EmailMessageReceivedEventHandler? _eventMessageReceivedHandler;
    private ILogger<EmailModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new EmailModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<EmailModule>>();
        _logger?.LogInformation("Initializing Email module ({ModuleId})", context.ModuleId);

        _eventBus = context.Services.GetRequiredService<IEventBus>();

        var handlerLogger = context.Services.GetService<ILogger<EmailAccountAddedEventHandler>>();
        _eventAccountAddedHandler = new EmailAccountAddedEventHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EmailAccountAddedEventHandler>.Instance);
        await _eventBus.SubscribeAsync(_eventAccountAddedHandler, cancellationToken);

        var msgHandlerLogger = context.Services.GetService<ILogger<EmailMessageReceivedEventHandler>>();
        _eventMessageReceivedHandler = new EmailMessageReceivedEventHandler(
            msgHandlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EmailMessageReceivedEventHandler>.Instance);
        await _eventBus.SubscribeAsync(_eventMessageReceivedHandler, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("Email module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            throw new InvalidOperationException("Module must be initialized before starting.");

        _running = true;
        _logger?.LogInformation("Email module started");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;

        if (_eventBus is not null)
        {
            if (_eventAccountAddedHandler is not null)
                await _eventBus.UnsubscribeAsync(_eventAccountAddedHandler, cancellationToken);
            if (_eventMessageReceivedHandler is not null)
                await _eventBus.UnsubscribeAsync(_eventMessageReceivedHandler, cancellationToken);
        }

        _logger?.LogInformation("Email module stopped");
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("Email module disposed");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());

    /// <summary>Gets whether the module has been initialized.</summary>
    public bool IsInitialized => _initialized;

    /// <summary>Gets whether the module is currently running.</summary>
    public bool IsRunning => _running;
}
