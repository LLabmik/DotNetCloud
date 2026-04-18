using DotNetCloud.Core.Events;
using DotNetCloud.Modules.Chat.Events;

namespace DotNetCloud.Modules.Chat.Services;

/// <summary>
/// Handles video call events and dispatches push notifications.
/// Combines event handlers for <see cref="VideoCallInitiatedEvent"/>,
/// <see cref="VideoCallMissedEvent"/>, and <see cref="VideoCallEndedEvent"/>.
/// </summary>
public interface ICallNotificationHandler
    : IEventHandler<VideoCallInitiatedEvent>,
      IEventHandler<VideoCallMissedEvent>,
      IEventHandler<VideoCallEndedEvent>
{
}
