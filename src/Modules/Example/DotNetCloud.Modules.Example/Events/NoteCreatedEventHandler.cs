using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Example.Events;

/// <summary>
/// Handles <see cref="NoteCreatedEvent"/> by logging the creation.
/// Demonstrates how modules subscribe to and react to domain events.
/// </summary>
public sealed class NoteCreatedEventHandler : IEventHandler<NoteCreatedEvent>
{
    private readonly ILogger<NoteCreatedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoteCreatedEventHandler"/> class.
    /// </summary>
    public NoteCreatedEventHandler(ILogger<NoteCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(NoteCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Note created: {NoteId} titled '{Title}' by user {UserId}",
            @event.NoteId,
            @event.Title,
            @event.CreatedByUserId);

        return Task.CompletedTask;
    }
}
