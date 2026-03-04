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
        nameof(UserLeftChannelEvent)
    };

    /// <inheritdoc />
    public IReadOnlyCollection<string> SubscribedEvents => new[]
    {
        "FileUploadedEvent"
    };
}
