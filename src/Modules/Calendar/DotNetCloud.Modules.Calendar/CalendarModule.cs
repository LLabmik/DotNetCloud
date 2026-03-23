using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Calendar.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar;

/// <summary>
/// Calendar module implementation.
/// Manages calendars, events, attendees, reminders, and CalDAV integration.
/// </summary>
public sealed class CalendarModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private CalendarEventCreatedEventHandler? _eventCreatedHandler;
    private ILogger<CalendarModule>? _logger;
    private bool _initialized;
    private bool _running;

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new CalendarModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<CalendarModule>>();
        _logger?.LogInformation("Initializing Calendar module ({ModuleId})", context.ModuleId);

        _eventBus = context.Services.GetRequiredService<IEventBus>();

        var handlerLogger = context.Services.GetService<ILogger<CalendarEventCreatedEventHandler>>();
        _eventCreatedHandler = new CalendarEventCreatedEventHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CalendarEventCreatedEventHandler>.Instance);
        await _eventBus.SubscribeAsync(_eventCreatedHandler, cancellationToken);

        _initialized = true;
        _logger?.LogInformation("Calendar module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("Calendar module started");

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

        _logger?.LogInformation("Calendar module stopped");
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _logger?.LogInformation("Calendar module disposed");
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
