using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Notes.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Notes;

/// <summary>
/// Notes module implementation.
/// Manages notes, folders, tags, version history, and sharing.
/// </summary>
public sealed class NotesModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private NoteCreatedEventHandler? _eventCreatedHandler;
    private ILogger<NotesModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new NotesModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<NotesModule>>();
        _logger?.LogInformation("Initializing Notes module ({ModuleId})", context.ModuleId);

        _eventBus = context.Services.GetRequiredService<IEventBus>();

        var handlerLogger = context.Services.GetService<ILogger<NoteCreatedEventHandler>>();
        _eventCreatedHandler = new NoteCreatedEventHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NoteCreatedEventHandler>.Instance);
        await _eventBus.SubscribeAsync(_eventCreatedHandler, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("Notes module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("Notes module started");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;

        if (_eventBus is not null && _eventCreatedHandler is not null)
        {
            await _eventBus.UnsubscribeAsync(_eventCreatedHandler, cancellationToken);
        }

        _logger?.LogInformation("Notes module stopped");
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("Notes module disposed");
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
