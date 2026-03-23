using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Calendar.Events;

/// <summary>
/// Handles <see cref="CalendarEventCreatedEvent"/> within the Calendar module.
/// </summary>
public sealed class CalendarEventCreatedEventHandler : IEventHandler<CalendarEventCreatedEvent>
{
    private readonly ILogger<CalendarEventCreatedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarEventCreatedEventHandler"/> class.
    /// </summary>
    public CalendarEventCreatedEventHandler(ILogger<CalendarEventCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(CalendarEventCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calendar event created: {CalendarEventId} '{Title}' by user {UserId} in calendar {CalendarId}",
            @event.CalendarEventId,
            @event.Title,
            @event.CreatedByUserId,
            @event.CalendarId);

        return Task.CompletedTask;
    }
}
