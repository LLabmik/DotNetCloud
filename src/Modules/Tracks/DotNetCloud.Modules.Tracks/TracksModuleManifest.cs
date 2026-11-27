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
    public string Version => "2.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.INotificationService),
        nameof(Core.Capabilities.IUserDirectory),
        nameof(Core.Capabilities.IAuditLogger),
        nameof(Core.Capabilities.IRealtimeBroadcaster),

        // Tracks is organization-scoped: the module UI resolves the acting user's organizations
        // through this capability (the host cannot read core.organizations).
        nameof(Core.Capabilities.IOrganizationDirectory)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(ProductCreatedEvent),
        nameof(ProductDeletedEvent),
        nameof(WorkItemCreatedEvent),
        nameof(WorkItemMovedEvent),
        nameof(WorkItemUpdatedEvent),
        nameof(WorkItemDeletedEvent),
        nameof(WorkItemAssignedEvent),
        nameof(WorkItemCommentAddedEvent),
        nameof(SprintStartedEvent),
        nameof(SprintCompletedEvent),
        nameof(PokerSessionStartedEvent),
        nameof(PokerSessionRevealedEvent),
        nameof(PokerSessionCompletedEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        "FileDeletedEvent",
        "MessageSentEvent",
        "ChannelCreatedEvent",
        "ChannelDeletedEvent"
    };
}
