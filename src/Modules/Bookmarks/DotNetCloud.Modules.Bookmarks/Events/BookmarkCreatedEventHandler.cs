using DotNetCloud.Core.Events;
using Microsoft.Extensions.Logging;

namespace DotNetCloud.Modules.Bookmarks.Events;

/// <summary>
/// Handles <see cref="BookmarkCreatedEvent"/> for the Bookmarks module.
/// </summary>
public sealed class BookmarkCreatedEventHandler : IEventHandler<BookmarkCreatedEvent>
{
    private readonly ILogger<BookmarkCreatedEventHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarkCreatedEventHandler"/> class.
    /// </summary>
    public BookmarkCreatedEventHandler(ILogger<BookmarkCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(BookmarkCreatedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bookmark created: {BookmarkId} '{Title}'", @event.BookmarkId, @event.Title);
        return Task.CompletedTask;
    }
}
