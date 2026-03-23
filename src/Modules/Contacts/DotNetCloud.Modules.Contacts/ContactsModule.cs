using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Contacts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Contacts;

/// <summary>
/// Contacts module implementation.
/// Manages contact records, groups, sharing, and CardDAV integration.
/// </summary>
public sealed class ContactsModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private ContactCreatedEventHandler? _contactCreatedHandler;
    private ILogger<ContactsModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new ContactsModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<ContactsModule>>();
        _logger?.LogInformation("Initializing Contacts module ({ModuleId})", context.ModuleId);

        _eventBus = context.Services.GetRequiredService<IEventBus>();

        var handlerLogger = context.Services.GetService<ILogger<ContactCreatedEventHandler>>();
        _contactCreatedHandler = new ContactCreatedEventHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ContactCreatedEventHandler>.Instance);
        await _eventBus.SubscribeAsync(_contactCreatedHandler, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("Contacts module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("Contacts module started");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;

        if (_eventBus is not null && _contactCreatedHandler is not null)
        {
            await _eventBus.UnsubscribeAsync(_contactCreatedHandler, cancellationToken);
        }

        _logger?.LogInformation("Contacts module stopped");
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("Contacts module disposed");
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
