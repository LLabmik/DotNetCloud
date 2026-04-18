using DotNetCloud.Core.Events;
using DotNetCloud.Core.Modules;
using DotNetCloud.Modules.Chat.Events;

namespace DotNetCloud.Modules.Chat;

/// <summary>
/// Manifest for the Chat module.
/// Declares identity, capabilities, and event contracts for the module system.
/// </summary>
public sealed class ChatModuleManifest : IModuleManifest
{
    /// <inheritdoc />
    public string Id => "dotnetcloud.chat";

    /// <inheritdoc />
    public string Name => "Chat";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiredCapabilities => new[]
    {
        nameof(Core.Capabilities.INotificationService),
        nameof(Core.Capabilities.IUserDirectory),
        nameof(Core.Capabilities.ICurrentUserContext),
        nameof(Core.Capabilities.IRealtimeBroadcaster)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> PublishedEvents => new[]
    {
        nameof(MessageSentEvent),
        nameof(ChannelCreatedEvent),
        nameof(ChannelDeletedEvent),
        nameof(UserJoinedChannelEvent),
        nameof(UserLeftChannelEvent),
        nameof(PresenceChangedEvent),
        nameof(VideoCallInitiatedEvent),
        nameof(VideoCallAnsweredEvent),
        nameof(VideoCallEndedEvent),
        nameof(VideoCallMissedEvent),
        nameof(ParticipantJoinedCallEvent),
        nameof(ParticipantLeftCallEvent),
        nameof(ScreenShareStartedEvent),
        nameof(ScreenShareEndedEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        "FileUploadedEvent",
        nameof(CardCreatedEvent),
        nameof(CardMovedEvent),
        nameof(CardUpdatedEvent),
        nameof(CardDeletedEvent),
        nameof(CardAssignedEvent),
        nameof(CardCommentAddedEvent),
        nameof(SprintStartedEvent),
        nameof(SprintCompletedEvent),
        nameof(BoardCreatedEvent),
        nameof(BoardDeletedEvent)
    };
}
