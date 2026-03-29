using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;

namespace DotNetCloud.Modules.Tracks;

/// <summary>
/// Module manifest for the Tracks module.
/// Declares capabilities, published events, and subscribed events.
/// </summary>
public sealed class TracksModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.tracks";

    /// <inheritdoc />
    public string Name => "Tracks";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.INotificationService),
        nameof(Core.Capabilities.IUserDirectory),
        nameof(Core.Capabilities.ICurrentUserContext),
        nameof(Core.Capabilities.IAuditLogger),
        nameof(Core.Capabilities.IRealtimeBroadcaster)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(BoardCreatedEvent),
        nameof(BoardDeletedEvent),
        nameof(CardCreatedEvent),
        nameof(CardMovedEvent),
        nameof(CardUpdatedEvent),
        nameof(CardDeletedEvent),
        nameof(CardAssignedEvent),
        nameof(CardCommentAddedEvent),
        nameof(SprintStartedEvent),
        nameof(SprintCompletedEvent),
        nameof(PokerSessionStartedEvent),
        nameof(PokerSessionRevealedEvent),
        nameof(PokerSessionCompletedEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        "FileDeletedEvent"
    };
}
