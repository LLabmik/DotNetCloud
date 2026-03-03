using DotNetCloud.Core.Authorization;
using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Example.Events;
using DotNetCloud.Modules.Example.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Example;

/// <summary>
/// Reference implementation of a DotNetCloud module.
/// Demonstrates the full module lifecycle: initialization, start, stop, and disposal.
/// </summary>
/// <remarks>
/// This module manages simple notes and serves as a reference for third-party module authors.
/// It demonstrates:
/// <list type="bullet">
///   <item><description>Implementing <see cref="IModuleLifecycle"/> for full lifecycle control</description></item>
///   <item><description>Declaring capabilities and events via <see cref="ExampleModuleManifest"/></description></item>
///   <item><description>Subscribing to and publishing domain events</description></item>
///   <item><description>Using <see cref="ModuleInitializationContext"/> for DI and configuration</description></item>
///   <item><description>Proper async disposal of resources</description></item>
/// </list>
/// </remarks>
public sealed class ExampleModule : IModuleLifecycle
{
    private IEventBus? _eventBus;
    private NoteCreatedEventHandler? _noteCreatedHandler;
    private ILogger<ExampleModule>? _logger;
    private bool _initialized;
    private bool _running;
    private readonly List<ExampleNote> _notes = [];

    /// <inheritdoc />
    public IModuleManifest Manifest { get; } = new ExampleModuleManifest();

    /// <inheritdoc />
    public async Task InitializeAsync(ModuleInitializationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger = context.Services.GetService<ILogger<ExampleModule>>();
        _logger?.LogInformation("Initializing Example module ({ModuleId})", context.ModuleId);

        // Resolve the event bus from DI
        _eventBus = context.Services.GetRequiredService<IEventBus>();

        // Create and register event handler
        var handlerLogger = context.Services.GetService<ILogger<NoteCreatedEventHandler>>();
        _noteCreatedHandler = new NoteCreatedEventHandler(
            handlerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NoteCreatedEventHandler>.Instance);

        await _eventBus.SubscribeAsync(_noteCreatedHandler, cancellationToken);

        // Load module-specific configuration
        if (context.Configuration.TryGetValue("max_notes", out var maxNotesObj))
        {
            _logger?.LogInformation("Max notes configured: {MaxNotes}", maxNotesObj);
        }

        _initialized = true;
        _logger?.LogInformation("Example module initialized successfully");
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Module must be initialized before starting.");
        }

        _running = true;
        _logger?.LogInformation("Example module started");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _running = false;

        // Unsubscribe from events
        if (_eventBus is not null && _noteCreatedHandler is not null)
        {
            await _eventBus.UnsubscribeAsync(_noteCreatedHandler, cancellationToken);
        }

        _logger?.LogInformation("Example module stopped");
    }

    /// <inheritdoc />
    public Task DisposeAsync()
    {
        _notes.Clear();
        _logger?.LogInformation("Example module disposed");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return new ValueTask(DisposeAsync());
    }

    /// <summary>
    /// Creates a new note and publishes a <see cref="NoteCreatedEvent"/>.
    /// Demonstrates how modules create data and publish events.
    /// </summary>
    /// <param name="title">The note title.</param>
    /// <param name="content">The note body content.</param>
    /// <param name="userId">The user creating the note.</param>
    /// <param name="caller">The caller context for authorization.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created note.</returns>
    public async Task<ExampleNote> CreateNoteAsync(
        string title,
        string content,
        Guid userId,
        CallerContext caller,
        CancellationToken cancellationToken = default)
    {
        if (!_running)
        {
            throw new InvalidOperationException("Module is not running.");
        }

        var note = new ExampleNote
        {
            Title = title,
            Content = content,
            CreatedByUserId = userId
        };

        _notes.Add(note);

        // Publish domain event
        if (_eventBus is not null)
        {
            var @event = new NoteCreatedEvent
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                NoteId = note.Id,
                Title = note.Title,
                CreatedByUserId = userId
            };

            await _eventBus.PublishAsync(@event, caller, cancellationToken);
        }

        _logger?.LogInformation("Note created: {NoteId}", note.Id);
        return note;
    }

    /// <summary>
    /// Gets all notes. Demonstrates a simple query operation.
    /// </summary>
    /// <returns>A read-only list of all notes.</returns>
    public IReadOnlyList<ExampleNote> GetNotes()
    {
        if (!_running)
        {
            throw new InvalidOperationException("Module is not running.");
        }

        return _notes.AsReadOnly();
    }

    /// <summary>
    /// Gets a note by its identifier.
    /// </summary>
    /// <param name="noteId">The note identifier.</param>
    /// <returns>The note, or null if not found.</returns>
    public ExampleNote? GetNote(Guid noteId)
    {
        if (!_running)
        {
            throw new InvalidOperationException("Module is not running.");
        }

        return _notes.Find(n => n.Id == noteId);
    }
}
